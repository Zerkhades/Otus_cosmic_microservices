using MediatR;
using System.Text.Json;
using TournamentService.Domains;
using TournamentService.Infrastructure.Repositories;
using TournamentService.Infrastructure.Repositories.Kafka;

namespace TournamentService.Application.Commands;

public record CreateTournamentCommand(string Name, DateTime StartsAt, int MaxPlayers, string RuleSetJson) : IRequest<Guid>;

public class CreateTournamentCommandHandler : IRequestHandler<CreateTournamentCommand, Guid>
{
    private readonly ITournamentRepository _repo;
    private readonly IKafkaProducerWrapper _producer;
    private readonly ILogger<CreateTournamentCommandHandler> _logger;

    public CreateTournamentCommandHandler(
        ITournamentRepository repo, 
        IKafkaProducerWrapper producer,
        ILogger<CreateTournamentCommandHandler> logger)
    {
        _repo = repo;
        _producer = producer;
        _logger = logger;
    }

    public async Task<Guid> Handle(CreateTournamentCommand request, CancellationToken cancellationToken)
    {
        var entity = new Tournament
        {
            Name = request.Name,
            StartsAt = request.StartsAt,
            MaxPlayers = request.MaxPlayers,
            RuleSetJson = request.RuleSetJson,
            Status = TournamentStatus.Upcoming
        };

        try
        {
            await _repo.InsertAsync(entity, cancellationToken);
            
            // Create a structured event payload with more details
            var eventPayload = new
            {
                tournamentId = entity.Id,
                name = entity.Name,
                startsAt = entity.StartsAt,
                maxPlayers = entity.MaxPlayers,
                status = entity.Status.ToString()
            };
            
            // Serialize to JSON
            var jsonPayload = JsonSerializer.Serialize(eventPayload);
            
            await _producer.PublishAsync("tournament.created", jsonPayload, cancellationToken);
            return entity.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tournament {Name}", request.Name);
            throw;
        }
    }
}