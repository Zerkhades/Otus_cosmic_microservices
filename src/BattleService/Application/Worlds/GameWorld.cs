using BattleService.Extentions;
using BattleService.GameLogic.Commands;
using BattleService.GameLogic.Engine;
using BattleService.Protos;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BattleService.Application.Worlds;

/// <summary>Один мир на один battleId. Имеет свой игровой цикл (20 Гц).</summary>
public sealed class GameWorld
{
    private readonly object _clientsLock = new();
    private readonly HashSet<Client> _clients = new();
    private readonly ConcurrentQueue<Action<GameLoop>> _inbox = new();

    private readonly IGameLoopFactory _factory;
    private readonly ILogger _log;

    private Task? _loopTask;
    private CancellationTokenSource? _cts;

    // Параметры цикла
    private const double TickDt = 0.05; // 20 Гц
    private static readonly TimeSpan _tickDelay = TimeSpan.FromMilliseconds(50);

    public Guid BattleId { get; }
    public int ClientsCount { get { lock (_clientsLock) return _clients.Count; } }
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;

    private readonly GameLoop _loop;

    public GameWorld(Guid battleId, IGameLoopFactory factory, ILogger log)
    {
        BattleId = battleId;
        _factory = factory;
        _log = log;
        _loop = _factory.Create(battleId);
    }

    public void Start()
    {
        if (_loopTask != null) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public void Stop()
    {
        try { _cts?.Cancel(); } catch { /* ignore */ }
    }

    public async Task StopAsync()
    {
        // Опц. метод для аккуратной остановки (не ломаем существующие вызовы Stop())
        try { _cts?.Cancel(); } catch { /* ignore */ }
        if (_loopTask != null)
        {
            try { await _loopTask.ConfigureAwait(false); } catch { /* ignore */ }
        }
    }

    public Client AddConnection(IServerStreamWriter<ServerUpdate> stream, string playerId)
    {
        var c = new Client(stream, playerId);
        lock (_clientsLock) _clients.Add(c);
        LastActivityUtc = DateTime.UtcNow;
        return c;
    }

    public void RemoveConnection(Client c)
    {
        lock (_clientsLock) _clients.Remove(c);
        LastActivityUtc = DateTime.UtcNow;
    }

    /// <summary>Принимаем полезную нагрузку от клиента, раскладываем в очередь действий над GameLoop.</summary>
    public void AcceptPayload(string playerId, ReadOnlySpan<byte> payload)
    {
        LastActivityUtc = DateTime.UtcNow;

        if (!Guid.TryParse(playerId, out var playerGuid))
        {
            _log.LogWarning("Bad playerId format for battle {BattleId}: {PlayerId}", BattleId, playerId);
            return;
        }

        try
        {
            // Используем byte[] для совместимости с доступными перегрузками JsonDocument.Parse
            var jsonBytes = payload.ToArray();
            using var doc = JsonDocument.Parse(jsonBytes);
            var root = doc.RootElement;

            // Поддерживаем ДВА формата:
            // 1) новый: { kind:'cmd', battleId, tick, commands:[ {type:'TURN'|...} ] }
            // 2) старый: { cmd:'move'|'turn'|'shoot', ... }
            if (root.TryGetProperty("kind", out var kindProp) &&
                kindProp.ValueEquals("cmd") &&
                root.TryGetProperty("commands", out var commands) &&
                commands.ValueKind == JsonValueKind.Array)
            {
                foreach (var j in commands.EnumerateArray())
                {
                    if (!j.TryGetProperty("type", out var typeProp)) continue;
                    var type = typeProp.GetString();
                    switch (type)
                    {
                        case "TURN":
                            if (j.TryGetProperty("value", out var vProp))
                            {
                                var v = vProp.GetInt32(); // -1,0,1
                                _inbox.Enqueue(gl => gl.Enqueue(new TurnCommand(playerGuid, v * (float)(180.0 * TickDt))));
                            }
                            break;

                        case "MOVE":
                            var thrust = j.TryGetProperty("thrust", out var thrustProp) && thrustProp.GetBoolean();
                            var brake = j.TryGetProperty("brake", out var brakeProp) && brakeProp.GetBoolean();
                            // пускай MoveCommand принимает дельту тяги: thrust=+1, brake=-1, иначе 0
                            var delta = thrust ? 1f : brake ? -1f : 0f;
                            _inbox.Enqueue(gl => gl.Enqueue(new MoveCommand(playerGuid, delta)));
                            break;

                        case "SHOOT":
                            _inbox.Enqueue(gl => gl.Enqueue(new ShootCommand(playerGuid, "primary")));
                            break;
                    }
                }
            }
            else if (root.TryGetProperty("cmd", out var legacyCmd))
            {
                switch (legacyCmd.GetString())
                {
                    case "turn":
                        if (root.TryGetProperty("deg", out var degProp))
                        {
                            var deg = degProp.GetSingle();
                            _inbox.Enqueue(gl => gl.Enqueue(new TurnCommand(playerGuid, deg)));
                        }
                        break;
                    case "move":
                        if (root.TryGetProperty("delta", out var dProp))
                        {
                            var d = dProp.GetSingle();
                            _inbox.Enqueue(gl => gl.Enqueue(new MoveCommand(playerGuid, d)));
                        }
                        break;
                    case "shoot":
                        var w = root.TryGetProperty("weapon", out var wp) ? wp.GetString() ?? "primary" : "primary";
                        _inbox.Enqueue(gl => gl.Enqueue(new ShootCommand(playerGuid, w)));
                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            _log.LogWarning(ex, "Bad JSON payload for battle {BattleId}", BattleId);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Bad payload for battle {BattleId}", BattleId);
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // применяем накопленные действия к GameLoop (единственный поток)
                while (_inbox.TryDequeue(out var op))
                    op(_loop);

                _loop.Tick((float)TickDt);

                // сериализуем снапшот и вещаем всем
                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    _loop.Snapshot,
                    GameContextJsonContext.Default.GameContext
                );

                List<Client> copy;
                lock (_clientsLock) copy = _clients.ToList();

                var tick = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 50);
                foreach (var c in copy)
                {
                    try
                    {
                        var update = new ServerUpdate
                        {
                            Tick = tick,
                            State = ByteString.CopyFrom(bytes)
                        };
                        await c.Stream.WriteAsync(update, ct);
                    }
                    catch
                    {
                        // клиент умер — удаляем
                        RemoveConnection(c);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "World loop error for battle {BattleId}", BattleId);
            }

            try { await Task.Delay(_tickDelay, ct); } catch { }
        }
    }

    public void EnsurePlayer(string playerId)
    {
        if (!Guid.TryParse(playerId, out var guid))
        {
            _log.LogWarning("Bad playerId format for EnsurePlayer in battle {BattleId}: {PlayerId}", BattleId, playerId);
            return;
        }
        _inbox.Enqueue(gl => gl.RegisterPlayer(guid));
    }

    public async Task WriteSnapshotOnceAsync(Client c, CancellationToken ct)
    {
        // сериализуем текущий снапшот и шлём одному клиенту
        var bytes = JsonSerializer.SerializeToUtf8Bytes(
            _loop.Snapshot,
            GameContextJsonContext.Default.GameContext
        );
        await c.Stream.WriteAsync(new ServerUpdate
        {
            Tick = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 50),
            State = Google.Protobuf.ByteString.CopyFrom(bytes)
        }, ct);
    }

    public sealed class Client
    {
        public IServerStreamWriter<ServerUpdate> Stream { get; }
        public string PlayerId { get; }
        public Client(IServerStreamWriter<ServerUpdate> stream, string playerId)
        {
            Stream = stream; PlayerId = playerId;
        }
    }
}
