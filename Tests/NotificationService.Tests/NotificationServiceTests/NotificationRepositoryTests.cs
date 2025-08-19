using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using NotificationService.Domains;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Repositories;

namespace NotificationServiceTests;

public class NotificationRepositoryTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Add_And_GetUnread_Work_AsExpected()
    {
        using var db = CreateDb();
        var repo = new NotificationRepository(db);

        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();

        var n1 = new Notification { RecipientId = u1, Type = "battle.finished", IsSent = false };
        var n2 = new Notification { RecipientId = u1, Type = "battle.finished", IsSent = true };
        var n3 = new Notification { RecipientId = u2, Type = "tournament.registration", IsSent = false };

        await repo.AddAsync(n1, CancellationToken.None);
        await repo.AddAsync(n2, CancellationToken.None);
        await repo.AddAsync(n3, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        var unreadU1 = await repo.GetUnreadAsync(u1, CancellationToken.None);
        Assert.Single(unreadU1);
        Assert.All(unreadU1, n =>
        {
            Assert.Equal(u1, n.RecipientId);
            Assert.False(n.IsSent);
        });

        var unreadU2 = await repo.GetUnreadAsync(u2, CancellationToken.None);
        Assert.Single(unreadU2);
        Assert.Equal(u2, unreadU2.First().RecipientId);
    }

    [Fact]
    public async Task MarkAsSent_UpdatesEntity_WhenExists()
    {
        using var db = CreateDb();
        var repo = new NotificationRepository(db);

        var user = Guid.NewGuid();
        var n = new Notification { RecipientId = user, Type = "battle.finished", IsSent = false };

        await repo.AddAsync(n, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        await repo.MarkAsSentAsync(n.Id, CancellationToken.None);

        var reloaded = await db.Notifications.FindAsync(n.Id);
        Assert.NotNull(reloaded);
        Assert.True(reloaded!.IsSent);
    }

    [Fact]
    public async Task MarkAsSent_DoesNothing_WhenNotFound()
    {
        using var db = CreateDb();
        var repo = new NotificationRepository(db);

        // Не должен кидать и не должен ничего менять
        await repo.MarkAsSentAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(db.Notifications);
    }
}