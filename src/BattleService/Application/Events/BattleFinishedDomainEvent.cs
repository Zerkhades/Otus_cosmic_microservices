using MediatR;

namespace BattleService.Application.Events;

public record BattleFinishedDomainEvent(Guid BattleId, Guid TournamentId, List<Guid> Participants) : INotification;