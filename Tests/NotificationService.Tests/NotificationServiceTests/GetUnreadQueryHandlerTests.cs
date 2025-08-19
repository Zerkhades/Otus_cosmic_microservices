using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application.Queries;
using NotificationService.Domains;
using NotificationService.Infrastructure.Repositories;

namespace NotificationServiceTests;

public class GetUnreadQueryHandlerTests
{
    private sealed class FakeRepo : INotificationRepository
    {
        public Guid? LastRequestedRecipientId { get; private set; }
        public List<Notification> Storage { get; } = new();

        public Task AddAsync(Notification entity, CancellationToken ct)
        {
            Storage.Add(entity);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task<IEnumerable<Notification>> GetUnreadAsync(Guid recipientId, CancellationToken ct)
        {
            LastRequestedRecipientId = recipientId;
            IEnumerable<Notification> result = Storage.FindAll(n => n.RecipientId == recipientId && !n.IsSent);
            return Task.FromResult(result);
        }

        public Task MarkAsSentAsync(Guid id, CancellationToken ct)
        {
            var n = Storage.Find(x => x.Id == id);
            if (n != null) n.IsSent = true;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_Returns_Unread_For_RequestedRecipient()
    {
        var repo = new FakeRepo();
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();

        repo.Storage.AddRange(new[]
        {
            new Notification { RecipientId = u1, Type = "battle.finished", IsSent = false },
            new Notification { RecipientId = u1, Type = "battle.finished", IsSent = true },
            new Notification { RecipientId = u2, Type = "battle.finished", IsSent = false },
        });

        var handler = new GetUnreadQueryHandler(repo, NullLogger<GetUnreadQueryHandler>.Instance);

        var unread = await handler.Handle(new GetUnreadQuery(u1), CancellationToken.None);

        Assert.Equal(u1, repo.LastRequestedRecipientId);
        var list = Assert.IsType<List<Notification>>(unread as List<Notification> ?? unread.ToList());
        Assert.Single(list);
        Assert.All(list, n =>
        {
            Assert.Equal(u1, n.RecipientId);
            Assert.False(n.IsSent);
        });
    }
}