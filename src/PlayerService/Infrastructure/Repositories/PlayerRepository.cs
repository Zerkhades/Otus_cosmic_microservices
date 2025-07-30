using Microsoft.EntityFrameworkCore;
using PlayerService.Domains;
using PlayerService.Infrastructure.Persistence;

namespace PlayerService.Infrastructure.Repositories;

public class PlayerRepository : IPlayerRepository
{
    private readonly AppDbContext _db;
    
    public PlayerRepository(AppDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(Player player, CancellationToken ct) => 
        _db.Players.AddAsync(player, ct).AsTask();

    public Task<Player?> FindAsync(Guid id, CancellationToken ct) => 
        _db.Players.FirstOrDefaultAsync(p => p.Id == id, ct);

    public Task SaveChangesAsync(CancellationToken ct) => 
        _db.SaveChangesAsync(ct);
}