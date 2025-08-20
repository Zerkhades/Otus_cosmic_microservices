using BattleService.Domains;
using BattleService.Infrastructure.State;
using MediatR;

namespace BattleService.Application.Commands;

public record StartBattleCommand(Guid BattleId, Guid TournamentId, List<Guid> Participants) : IRequest;

public class StartBattleCommandHandler : IRequestHandler<StartBattleCommand>
{
    private readonly IBattleStore _store;
    private readonly ILogger<StartBattleCommandHandler> _logger;

    public StartBattleCommandHandler(IBattleStore store, ILogger<StartBattleCommandHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public Task Handle(StartBattleCommand request, CancellationToken cancellationToken)
    {
        // TODO: Add customexception handling for better error management
        try
        {
            var battle = new Battle
            {
                Id = request.BattleId,
                TournamentId = request.TournamentId,
                Participants = request.Participants
            };
            battle.Start();
            _store.Add(battle);
            
            _logger.LogInformation("Battle {BattleId} started with {ParticipantCount} participants", 
                battle.Id, battle.Participants.Count);
                
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start battle {BattleId}", request.BattleId);
            throw;
        }
    }
}