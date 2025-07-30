using Microsoft.EntityFrameworkCore;
using NotificationService.Domains;

namespace NotificationService.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(b =>
        {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.RecipientId);
            b.Property(x => x.Type).HasMaxLength(64);
        });
    }
}