using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlayerService.Application.Commands;
using PlayerService.Domains;
using PlayerService.Infrastructure.Repositories;
using Xunit;

namespace PlayerService.Tests;

public class CreatePlayerCommandHandlerTests
{
    private sealed class FakePlayerRepository : IPlayerRepository
    {
        public Player? LastAdded { get; private set; }
        public int SaveChangesCalls { get; private set; }
        public CancellationToken LastAddCt { get; private set; }
        public CancellationToken LastSaveCt { get; private set; }

        private readonly Dictionary<Guid, Player> _store = new();

        public Task AddAsync(Player player, CancellationToken ct)
        {
            LastAdded = player;
            LastAddCt = ct;
            _store[player.Id] = player;
            return Task.CompletedTask;
        }

        public Task<Player?> FindAsync(Guid id, CancellationToken ct)
        {
            _store.TryGetValue(id, out var value);
            return Task.FromResult<Player?>(value);
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            SaveChangesCalls++;
            LastSaveCt = ct;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeKafkaProducerWrapper : IKafkaProducerWrapper
    {
        public readonly List<(string Topic, string Payload, CancellationToken Ct)> Published = new();

        public Task PublishAsync(string topic, string payload, CancellationToken ct)
        {
            Published.Add((topic, payload, ct));
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_Creates_Player_Saves_And_Publishes_Event()
    {
        var repo = new FakePlayerRepository();
        var producer = new FakeKafkaProducerWrapper();
        var handler = new CreatePlayerCommandHandler(repo, producer);

        using var cts = new CancellationTokenSource();
        var id = await handler.Handle(new CreatePlayerCommand("dave"), cts.Token);

        Assert.NotEqual(Guid.Empty, id);

        Assert.NotNull(repo.LastAdded);
        Assert.Equal("dave", repo.LastAdded!.UserName);
        Assert.Equal(1, repo.SaveChangesCalls);

        Assert.Single(producer.Published);
        var evt = producer.Published[0];
        Assert.Equal("player.registered", evt.Topic);
        Assert.Equal(id.ToString(), evt.Payload);

        // проверяем, что токен пробрасывается
        Assert.Equal(cts.Token, repo.LastAddCt);
        Assert.Equal(cts.Token, repo.LastSaveCt);
        Assert.Equal(cts.Token, evt.Ct);
    }
}