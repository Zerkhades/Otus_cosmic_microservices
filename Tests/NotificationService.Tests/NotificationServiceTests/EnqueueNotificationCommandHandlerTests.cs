using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Application.Commands;
using NotificationService.Domains;
using NotificationService.Infrastructure.Hubs;
using NotificationService.Infrastructure.Repositories;
using Xunit;

namespace NotificationServiceTests;

public class EnqueueNotificationCommandHandlerTests
{
    private sealed class FakeRepo : INotificationRepository
    {
        public readonly List<Notification> Storage = new();
        public readonly List<Guid> MarkedAsSent = new();
        public int SaveChangesCalled;

        public Task AddAsync(Notification entity, CancellationToken ct)
        {
            // Присваиваем Id, имитируя поведение EF (ValueGeneratedOnAdd)
            var idProp = entity.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
            if (idProp == null) throw new InvalidOperationException("Notification.Id not found.");
            var current = (Guid)(idProp.GetValue(entity) ?? Guid.Empty);
            if (current == Guid.Empty)
            {
                idProp.SetValue(entity, Guid.NewGuid());
            }

            Storage.Add(entity);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveChangesCalled++;
            return Task.CompletedTask;
        }

        public Task<IEnumerable<Notification>> GetUnreadAsync(Guid recipientId, CancellationToken ct) =>
            Task.FromResult(Storage.Where(n => n.RecipientId == recipientId && !n.IsSent));

        public Task MarkAsSentAsync(Guid id, CancellationToken ct)
        {
            MarkedAsSent.Add(id);
            var n = Storage.FirstOrDefault(x => GetId(x) == id);
            if (n != null) n.IsSent = true;
            return Task.CompletedTask;
        }

        private static Guid GetId(Notification n)
        {
            var idProp = n.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public)!;
            return (Guid)(idProp.GetValue(n) ?? Guid.Empty);
        }
    }

    private sealed class CapturingClientProxy : IClientProxy
    {
        public string? LastMethod { get; private set; }
        public object?[]? LastArgs { get; private set; }
        public CancellationToken LastToken { get; private set; }
        public bool ThrowOnSend { get; set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSend)
                throw new InvalidOperationException("send failed");

            LastMethod = method;
            LastArgs = args;
            LastToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubClients : IHubClients
    {
        private readonly CapturingClientProxy _proxy;
        public string? LastUserId { get; private set; }

        public FakeHubClients(CapturingClientProxy proxy) => _proxy = proxy;

        public IClientProxy All => throw new NotImplementedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Client(string connectionId) => throw new NotImplementedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotImplementedException();
        public IClientProxy Group(string groupName) => throw new NotImplementedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotImplementedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotImplementedException();
        public IClientProxy User(string userId)
        {
            LastUserId = userId;
            return _proxy;
        }
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotImplementedException();
    }

    private sealed class FakeHubContext : IHubContext<NotificationHub>
    {
        public IHubClients Clients { get; }
        public IGroupManager Groups { get; } = new NoopGroupManager();

        public FakeHubContext(IHubClients clients) => Clients = clients;

        private sealed class NoopGroupManager : IGroupManager
        {
            public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_Saves_Sends_And_Marks_As_Sent_On_Success()
    {
        var repo = new FakeRepo();
        var proxy = new CapturingClientProxy();
        var clients = new FakeHubClients(proxy);
        var hubContext = new FakeHubContext(clients);

        var handler = new EnqueueNotificationCommandHandler(repo, hubContext, NullLogger<EnqueueNotificationCommandHandler>.Instance);

        var recipient = Guid.NewGuid();
        var cmd = new EnqueueNotificationCommand(recipient, "battle.finished", "{\"x\":1}");

        var id = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, repo.SaveChangesCalled);
        Assert.Contains(repo.Storage, n => n.RecipientId == recipient && n.Type == "battle.finished");
        Assert.Contains(repo.MarkedAsSent, id2 => id2 == id);

        Assert.Equal(recipient.ToString(), clients.LastUserId);
        Assert.Equal("notify", proxy.LastMethod);
        Assert.NotNull(proxy.LastArgs);
        Assert.Equal("battle.finished", proxy.LastArgs![0]);

        var payloadObj = proxy.LastArgs[1];
        var textProp = payloadObj!.GetType().GetProperty("text");
        Assert.NotNull(textProp);
        Assert.Equal(cmd.PayloadJson, textProp!.GetValue(payloadObj) as string);
    }

    [Fact]
    public async Task Handle_Catches_Send_Error_And_Does_Not_Mark_As_Sent()
    {
        var repo = new FakeRepo();
        var proxy = new CapturingClientProxy { ThrowOnSend = true };
        var clients = new FakeHubClients(proxy);
        var hubContext = new FakeHubContext(clients);

        var handler = new EnqueueNotificationCommandHandler(repo, hubContext, NullLogger<EnqueueNotificationCommandHandler>.Instance);

        var recipient = Guid.NewGuid();
        var cmd = new EnqueueNotificationCommand(recipient, "tournament.registration", "{\"ok\":true}");

        var id = await handler.Handle(cmd, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, repo.SaveChangesCalled);
        Assert.Empty(repo.MarkedAsSent); // не помечаем как отправленное
    }
}