//using BattleService.Application.Commands;
//using BattleService.Extentions;
//using BattleService.GameLogic.Commands;
//using BattleService.GameLogic.Engine;
//using BattleService.Protos;
//using Google.Protobuf;
//using Grpc.Core;
//using MediatR;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.Extensions.Logging;
//using System.Text.Json;

//namespace BattleService.Services;

///// <summary>
///// gRPC-эндпоинт для синхронизации боя с Agent-приложением.
///// </summary>
//public sealed class BattleSyncService : BattleSynchronizer.BattleSynchronizerBase
//{
//    private readonly IMediator _mediator;
//    private readonly GameLoop _loop;
//    private readonly ILogger<BattleSyncService> _logger;

//    // интервал физического тика (50 мс ≈ 20 FPS)
//    private const float TickDt = 0.05f;

//    public BattleSyncService(IMediator mediator,
//                             GameLoop loop,
//                             ILogger<BattleSyncService> logger)
//    {
//        _mediator = mediator;
//        _loop = loop;
//        _logger = logger;
//    }

//    [Authorize]
//    public override async Task Connect(
//        IAsyncStreamReader<AgentTurn> requestStream,
//        IServerStreamWriter<ServerUpdate> responseStream,
//        ServerCallContext context)
//    {
//        // ---------------- Проверяем заголовки ----------------
//        if (!context.RequestHeaders.TryGetValue("battle-id", out var battleIdHeader) ||
//            !Guid.TryParse(battleIdHeader, out var battleId))
//        {
//            throw new RpcException(new Status(StatusCode.InvalidArgument,
//                           "Missing or invalid battle-id header"));
//        }
//        _logger.LogInformation("New gRPC stream for battle {BattleId}", battleId);

//        // ---------------- Читаем входящий поток Agent -> Server ----------------
//        _ = Task.Run(async () =>
//        {
//            await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
//            {
//                try
//                {
//                    HandleAgentTurn(msg);                    // enqueue команд в GameLoop
//                    await _mediator.Send(                    // (опц.) журнал для аналитики
//                        new SubmitTurnCommand(battleId,
//                                              msg.PlayerId,
//                                              msg.Tick,
//                                              msg.Payload.ToByteArray()),
//                        context.CancellationToken);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogError(ex, "Error handling turn {Tick} from {Player}",
//                        msg.Tick, msg.PlayerId);
//                }
//            }
//        }, context.CancellationToken);

//        // ---------------- Цикл отправки обновлений Server -> Agent -------------
//        try
//        {
//            while (!context.CancellationToken.IsCancellationRequested)
//            {
//                _loop.Tick(TickDt);                 // продвигаем физику

//                var snapshot = JsonSerializer.SerializeToUtf8Bytes(_loop.Snapshot,
//                                 GameContextJsonContext.Default.GameContext);

//                await responseStream.WriteAsync(new ServerUpdate
//                {
//                    Tick = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 50),
//                    State = ByteString.CopyFrom(snapshot)
//                }, context.CancellationToken);

//                await Task.Delay(TimeSpan.FromSeconds(TickDt), context.CancellationToken);
//            }
//        }
//        catch (OperationCanceledException)
//        {
//            _logger.LogInformation("Client disconnected from battle {BattleId}", battleId);
//        }
//    }

//    // -----------------------------------------------------------------
//    //    Разбор JSON-payload от агента и превращение его в ICommands
//    // -----------------------------------------------------------------
//    private void HandleAgentTurn(AgentTurn msg)
//    {
//        if (msg.Payload.IsEmpty) return;

//        using var doc = JsonDocument.Parse(msg.Payload.ToByteArray());
//        var root = doc.RootElement;

//        var shipId = Guid.Parse(msg.PlayerId);

//        switch (root.GetProperty("cmd").GetString())
//        {
//            case "move":
//                var thrust = root.GetProperty("delta").GetSingle(); // -1..1
//                _loop.Enqueue(new MoveCommand(shipId, thrust));
//                break;

//            case "turn":
//                var deg = root.GetProperty("deg").GetSingle();      // ±180
//                _loop.Enqueue(new TurnCommand(shipId, deg));
//                break;

//            case "shoot":
//                var weapon = root.GetProperty("weapon").GetString();
//                _loop.Enqueue(new ShootCommand(shipId, weapon!));
//                break;
//        }
//    }
//}


using BattleService.Application.Commands;
using BattleService.Application.Worlds;
using BattleService.Extentions;
using BattleService.GameLogic.Engine;
using BattleService.Protos;
using Google.Protobuf;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace BattleService.Services;

public sealed class BattleSyncService : BattleSynchronizer.BattleSynchronizerBase
{
    private readonly BattleWorldManager _worlds;
    private readonly IMediator _mediator;
    private readonly ILogger<BattleSyncService> _logger;

    public BattleSyncService(BattleWorldManager worlds, IMediator mediator, ILogger<BattleSyncService> logger)
    {
        _worlds = worlds;
        _mediator = mediator;
        _logger = logger;
    }

    [Authorize]
    public override async Task Connect(
        IAsyncStreamReader<AgentTurn> requestStream,
        IServerStreamWriter<ServerUpdate> responseStream,
        ServerCallContext context)
    {
        if (!context.RequestHeaders.TryGetValue("battle-id", out var battleIdHeader) ||
            !Guid.TryParse(battleIdHeader, out var battleId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Missing or invalid battle-id header"));

        var playerId = context.GetHttpContext()?.User?.FindFirst("sub")?.Value!;
        var world = _worlds.GetOrCreate(battleId);
        var conn = world.AddConnection(responseStream, playerId);
        world.EnsurePlayer(playerId);

        // сразу шлём стартовый снапшот (без ожидания команд)
        await world.WriteSnapshotOnceAsync(conn, context.CancellationToken);

        // дальше читаем команды и складываем их в очередь
        try
        {
            await foreach (var msg in requestStream.ReadAllAsync(context.CancellationToken))
            {
                world.AcceptPayload(msg.PlayerId ?? playerId, msg.Payload.Span);

                // аудит в фоне (не блокируем стрим)
                //_ = Task.Run(() => _mediator.Send(new SubmitTurnCommand(
                //    battleId, msg.PlayerId ?? playerId, msg.Tick, msg.Payload.ToByteArray()
                //)), context.CancellationToken);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            world.RemoveConnection(conn);
        }

        try { await Task.Delay(Timeout.Infinite, context.CancellationToken); }
        catch (OperationCanceledException) { }
    }
}
