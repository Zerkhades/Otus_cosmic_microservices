using Microsoft.EntityFrameworkCore;
using PlayerService.Domains;

namespace PlayerService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    
    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.UserName).IsRequired().HasMaxLength(32);
        });
    }
}