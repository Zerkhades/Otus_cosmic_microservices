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
        try
        {
            var text = System.Text.Encoding.UTF8.GetString(payload);
            using var doc = JsonDocument.Parse(text);
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
                    var type = j.GetProperty("type").GetString();
                    switch (type)
                    {
                        case "TURN":
                            var v = j.GetProperty("value").GetInt32(); // -1,0,1
                            _inbox.Enqueue(gl => gl.Enqueue(new TurnCommand(Guid.Parse(playerId), v * (float)(180.0 * TickDt))));
                            break;

                        case "MOVE":
                            var thrust = j.GetProperty("thrust").GetBoolean();
                            var brake = j.GetProperty("brake").GetBoolean();
                            // пускай MoveCommand принимает дельту тяги: thrust=+1, brake=-1, иначе 0
                            var delta = thrust ? 1f : brake ? -1f : 0f;
                            _inbox.Enqueue(gl => gl.Enqueue(new MoveCommand(Guid.Parse(playerId), delta)));
                            break;

                        case "SHOOT":
                            _inbox.Enqueue(gl => gl.Enqueue(new ShootCommand(Guid.Parse(playerId), "primary")));
                            break;
                    }
                }
            }
            else if (root.TryGetProperty("cmd", out var legacyCmd))
            {
                switch (legacyCmd.GetString())
                {
                    case "turn":
                        var deg = root.GetProperty("deg").GetSingle();
                        _inbox.Enqueue(gl => gl.Enqueue(new TurnCommand(Guid.Parse(playerId), deg)));
                        break;
                    case "move":
                        var d = root.GetProperty("delta").GetSingle();
                        _inbox.Enqueue(gl => gl.Enqueue(new MoveCommand(Guid.Parse(playerId), d)));
                        break;
                    case "shoot":
                        var w = root.TryGetProperty("weapon", out var wp) ? wp.GetString() ?? "primary" : "primary";
                        _inbox.Enqueue(gl => gl.Enqueue(new ShootCommand(Guid.Parse(playerId), w)));
                        break;
                }
            }
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
                var snapshot = _loop.Snapshot;

                var bytes = JsonSerializer.SerializeToUtf8Bytes(
                    _loop.Snapshot,
                    GameContextJsonContext.Default.GameContext
                );

                List<Client> copy;
                lock (_clientsLock) copy = _clients.ToList();

                foreach (var c in copy)
                {
                    try
                    {
                        var update = new ServerUpdate
                        {
                            Tick = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 50),
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
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _log.LogError(ex, "World loop error for battle {BattleId}", BattleId);
            }

            try { await Task.Delay(_tickDelay, ct); } catch { }
        }
    }

    public void EnsurePlayer(string playerId)
    {
        // если в твоём GameLoop есть метод «создать/зарегистрировать игрока», вызови его
        // либо сделай ленивую инициализацию (если у тебя корабль создаётся при первой команде — форсируй тут)
        _inbox.Enqueue(gl =>
        {
            gl.RegisterPlayer(Guid.Parse(playerId)); 
        });
    }

    public async Task WriteSnapshotOnceAsync(Client c, CancellationToken ct)
    {
        // сериализуем текущий снапшот и шлём одному клиенту
        var bytes = JsonSerializer.SerializeToUtf8Bytes(_loop.Snapshot /* или с JsonContext */);
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
