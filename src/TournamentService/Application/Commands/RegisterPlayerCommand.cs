using MediatR;
using TournamentService.Infrastructure.Repositories;
using TournamentService.Infrastructure.Repositories.Kafka;

namespace TournamentService.Application.Commands;

public record RegisterPlayerCommand(Guid TournamentId, Guid PlayerId) : IRequest<bool>
{
    public record Body(Guid PlayerId);
}

public class RegisterPlayerCommandHandler : IRequestHandler<RegisterPlayerCommand, bool>
{
    private readonly ITournamentRepository _repo;
    private readonly IKafkaProducerWrapper _producer;

    public RegisterPlayerCommandHandler(ITournamentRepository repo, IKafkaProducerWrapper producer)
    {
        _repo = repo;
        _producer = producer;
    }

    public async Task<bool> Handle(RegisterPlayerCommand request, CancellationToken cancellationToken)
    {
        var tournament = await _repo.FindAsync(request.TournamentId, cancellationToken);
        if (tournament is null || tournament.Participants.Count >= tournament.MaxPlayers)
        {
            await _producer.PublishAsync("tournament.registration.rejected", 
                $"{{\"tournamentId\":\"{request.TournamentId}\",\"playerId\":\"{request.PlayerId}\"}}", 
                cancellationToken);
            return false;
        }
        
        if (tournament.Participants.Contains(request.PlayerId)) return true; // idempotent

        tournament.Participants.Add(request.PlayerId);
        await _repo.ReplaceAsync(tournament, cancellationToken);
        
        await _producer.PublishAsync("tournament.registration.accepted", 
            $"{{\"tournamentId\":\"{request.TournamentId}\",\"playerId\":\"{request.PlayerId}\"}}", 
            cancellationToken);
        return true;
    }
}