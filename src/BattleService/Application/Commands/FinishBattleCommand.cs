using System.Text.Json;
using BattleService.Application.Events;
using BattleService.Infrastructure.Kafka;
using BattleService.Infrastructure.State;
using MediatR;

namespace BattleService.Application.Commands;

public record FinishBattleCommand(Guid BattleId) : IRequest<bool>;

public class FinishBattleCommandHandler : IRequestHandler<FinishBattleCommand, bool>
{
    private readonly IBattleStore _store;
    private readonly IKafkaProducerWrapper _producer;
    private readonly ILogger<FinishBattleCommandHandler> _logger;
    private readonly IMediator _mediator;

    public FinishBattleCommandHandler(
        IBattleStore store, 
        IKafkaProducerWrapper producer, 
        ILogger<FinishBattleCommandHandler> logger,
        IMediator mediator)
    {
        _store = store;
        _producer = producer;
        _logger = logger;
        _mediator = mediator;
    }

    public async Task<bool> Handle(FinishBattleCommand request, CancellationToken cancellationToken)
    {
        if (!_store.TryGet(request.BattleId, out var battle))
        {
            _logger.LogWarning("Battle with ID {BattleId} not found", request.BattleId);
            return false;
        }

        if (battle.Status == Domains.BattleStatus.Finished)
        {
            _logger.LogInformation("Battle with ID {BattleId} is already finished", request.BattleId);
            return true; // Idempotent operation
        }

        // Change battle status to finished
        battle.Finish();
        
        // Publish domain event
        await _mediator.Publish(new BattleFinishedDomainEvent(battle.Id, battle.TournamentId, battle.Participants), cancellationToken);
        
        // Create event payload
        var eventPayload = new
        {
            battleId = battle.Id,
            tournamentId = battle.TournamentId,
            participants = battle.Participants,
            finalTick = battle.CurrentTick
        };
        
        // Publish Kafka event
        try
        {
            var json = JsonSerializer.Serialize(eventPayload);
            await _producer.PublishAsync("battle.finished", json, cancellationToken);
            _logger.LogInformation("Published battle.finished event for battle {BattleId}", battle.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish battle.finished event for battle {BattleId}", battle.Id);
            return false;
        }
    }
}