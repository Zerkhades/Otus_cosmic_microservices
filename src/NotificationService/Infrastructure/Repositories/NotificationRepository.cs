using Microsoft.EntityFrameworkCore;
using NotificationService.Domains;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Repositories;

public interface INotificationRepository
{
    Task AddAsync(Notification entity, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<IEnumerable<Notification>> GetUnreadAsync(Guid recipientId, CancellationToken ct);
    Task MarkAsSentAsync(Guid id, CancellationToken ct);
}

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;
    public NotificationRepository(AppDbContext db) => _db = db;

    public Task AddAsync(Notification entity, CancellationToken ct) => 
        _db.Notifications.AddAsync(entity, ct).AsTask();
    
    public Task SaveChangesAsync(CancellationToken ct) => 
        _db.SaveChangesAsync(ct);
    
    public async Task<IEnumerable<Notification>> GetUnreadAsync(Guid recipientId, CancellationToken ct) =>
        await _db.Notifications
            .Where(n => n.RecipientId == recipientId && !n.IsSent)
            .ToListAsync(ct);
    
    public async Task MarkAsSentAsync(Guid id, CancellationToken ct)
    {
        var notification = await _db.Notifications.FindAsync(new object[] { id }, ct);
        if (notification != null)
        {
            notification.IsSent = true;
            await _db.SaveChangesAsync(ct);
        }
    }
}