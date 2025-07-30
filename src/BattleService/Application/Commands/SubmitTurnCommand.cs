using BattleService.Domains;
using BattleService.Infrastructure.State;
using MediatR;

namespace BattleService.Application.Commands;

public record SubmitTurnCommand(Guid BattleId, string PlayerId, int Tick, byte[] Payload) : IRequest;

public class SubmitTurnCommandHandler : IRequestHandler<SubmitTurnCommand>
{
    private readonly IBattleStore _store;
    private readonly ILogger<SubmitTurnCommandHandler> _logger;

    public SubmitTurnCommandHandler(IBattleStore store, ILogger<SubmitTurnCommandHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task Handle(SubmitTurnCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!_store.TryGet(request.BattleId, out var battle))
            {
                _logger.LogWarning("Battle not found: {BattleId}", request.BattleId);
                throw new KeyNotFoundException($"Battle not found: {request.BattleId}");
            }
            
            battle.SubmitTurn(new Turn(request.PlayerId, request.Tick, request.Payload));
            _logger.LogDebug("Turn submitted for battle {BattleId}, player {PlayerId}, tick {Tick}", 
                request.BattleId, request.PlayerId, request.Tick);
            
            return Task.CompletedTask;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for battle {BattleId}", request.BattleId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting turn for battle {BattleId}", request.BattleId);
            throw;
        }
    }
}