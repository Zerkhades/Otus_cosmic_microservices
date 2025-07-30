using PlayerService.Domains;

namespace PlayerService.Infrastructure.Repositories;

public interface IPlayerRepository
{
    Task AddAsync(Player player, CancellationToken ct);
    Task<Player?> FindAsync(Guid id, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}