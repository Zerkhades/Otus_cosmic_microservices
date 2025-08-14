using BattleService.Application.Worlds;
using BattleService.Extentions;
using BattleService.Protos;
using Grpc.Core;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

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
        // 1) battleId из заголовка
        if (!context.RequestHeaders.TryGetValue("battle-id", out var battleIdHeader) ||
            !Guid.TryParse(battleIdHeader, out var battleId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Missing or invalid battle-id header"));
        }

        // 2) попытка взять playerId из токена (если аутентификация прошла)
        var httpUser = context.GetHttpContext()?.User;
        string? playerId =
            httpUser?.FindFirst("sub")?.Value ??
            httpUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // 3) если в токене нет — читаем ПЕРВОЕ сообщение, берём из него
        AgentTurn? firstTurn = null;
        if (string.IsNullOrWhiteSpace(playerId))
        {
            if (!await requestStream.MoveNext())
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "No initial message, cannot identify player"));
            }
            firstTurn = requestStream.Current;
            playerId = string.IsNullOrWhiteSpace(firstTurn.PlayerId)
                ? null
                : firstTurn.PlayerId;

            if (string.IsNullOrWhiteSpace(playerId))
                throw new RpcException(new Status(StatusCode.InvalidArgument, "PlayerId is required"));
        }

        _logger.LogInformation("gRPC stream opened: battle={BattleId}, player={PlayerId}", battleId, playerId);

        // 4) регистрируем соединение и создаём игрока в мире
        var world = _worlds.GetOrCreate(battleId);
        var conn = world.AddConnection(responseStream, playerId!);
        world.EnsurePlayer(playerId!); // гарантируем сущность корабля

        try
        {
            // 5) если уже прочитали первое сообщение — не теряем его
            if (firstTurn is not null)
            {
                world.AcceptPayload(playerId!, firstTurn.Payload.Span);
            }

            // 6) сразу шлём стартовый снапшот — клиент начнёт рисовать сразу
            await world.WriteSnapshotOnceAsync(conn, context.CancellationToken);

            // 7) основной цикл чтения команд агента
            while (await requestStream.MoveNext(context.CancellationToken))
            {
                var msg = requestStream.Current;

                // подстрахуемся: если агент прислал PlayerId, он должен совпадать
                var fromMsg = string.IsNullOrWhiteSpace(msg.PlayerId) ? playerId! : msg.PlayerId;
                world.AcceptPayload(fromMsg, msg.Payload.Span);

                // при необходимости — аудит в фоне
                // _ = _mediator.Send(new SubmitTurnCommand(battleId, fromMsg, msg.Tick, msg.Payload.ToByteArray()));
            }
        }
        catch (OperationCanceledException)
        {
            // нормальное завершение
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Connect stream (battle={BattleId}, player={PlayerId})", battleId, playerId);
            throw;
        }
        finally
        {
            world.RemoveConnection(conn);
            _logger.LogInformation("gRPC stream closed: battle={BattleId}, player={PlayerId}", battleId, playerId);
        }
    }
}
