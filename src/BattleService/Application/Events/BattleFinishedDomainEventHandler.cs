using MediatR;

namespace BattleService.Application.Events;

public class BattleFinishedDomainEventHandler : INotificationHandler<BattleFinishedDomainEvent>
{
    private readonly ILogger<BattleFinishedDomainEventHandler> _logger;

    public BattleFinishedDomainEventHandler(ILogger<BattleFinishedDomainEventHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task Handle(BattleFinishedDomainEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Battle {BattleId} for tournament {TournamentId} has finished with {ParticipantCount} participants",
            notification.BattleId,
            notification.TournamentId,
            notification.Participants.Count);
            
        // Additional processing can be added here if needed
        // Such as updating statistics, notifying other services, etc.
        
        return Task.CompletedTask;
    }
}