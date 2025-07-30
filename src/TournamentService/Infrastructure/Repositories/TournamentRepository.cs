using MongoDB.Driver;
using TournamentService.Domains;
using TournamentService.Infrastructure.Persistence;

namespace TournamentService.Infrastructure.Repositories;

public interface ITournamentRepository
{
    Task InsertAsync(Tournament entity, CancellationToken ct);
    Task ReplaceAsync(Tournament entity, CancellationToken ct);
    Task<Tournament?> FindAsync(Guid id, CancellationToken ct);
    Task<List<Tournament>> FilterUpcomingAsync(CancellationToken ct);
}

public class TournamentRepository : ITournamentRepository
{
    private readonly MongoContext _ctx;
    public TournamentRepository(MongoContext ctx) => _ctx = ctx;

    public Task InsertAsync(Tournament entity, CancellationToken ct) => _ctx.Tournaments.InsertOneAsync(entity, cancellationToken: ct);
    public Task ReplaceAsync(Tournament entity, CancellationToken ct) => _ctx.Tournaments.ReplaceOneAsync(t => t.Id == entity.Id, entity, cancellationToken: ct);
    public async Task<Tournament?> FindAsync(Guid id, CancellationToken ct) => (await _ctx.Tournaments.FindAsync(t => t.Id == id, cancellationToken: ct)).FirstOrDefault();
    public async Task<List<Tournament>> FilterUpcomingAsync(CancellationToken ct)
        => (await _ctx.Tournaments.FindAsync(t => t.Status == TournamentStatus.Upcoming, cancellationToken: ct)).ToList();
}