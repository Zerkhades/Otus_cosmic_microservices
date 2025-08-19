using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentService.Application.Commands;
using TournamentService.Domains;
using Xunit;

namespace TournamentService.Tests;

public class RegisterPlayerCommandHandlerTests
{
    [Fact]
    public async Task ReturnsFalse_And_PublishesRejected_WhenTournamentNotFound()
    {
        var repo = new InMemoryTournamentRepository();
        var producer = new FakeKafkaProducerWrapper();
        var handler = new RegisterPlayerCommandHandler(repo, producer);

        var ok = await handler.Handle(new RegisterPlayerCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(ok);
        Assert.Single(producer.Published);
        Assert.Equal("tournament.registration.rejected", producer.Published[0].Topic);
    }

    [Fact]
    public async Task ReturnsFalse_And_PublishesRejected_WhenTournamentIsFull()
    {
        var repo = new InMemoryTournamentRepository();
        var t = new Tournament
        {
            MaxPlayers = 1,
            Status = TournamentStatus.Upcoming
        };
        t.Participants.Add(Guid.NewGuid());
        repo.Items.Add(t);

        var producer = new FakeKafkaProducerWrapper();
        var handler = new RegisterPlayerCommandHandler(repo, producer);

        var ok = await handler.Handle(new RegisterPlayerCommand(t.Id, Guid.NewGuid()), CancellationToken.None);

        Assert.False(ok);
        Assert.Single(producer.Published);
        Assert.Equal("tournament.registration.rejected", producer.Published[0].Topic);
    }

    [Fact]
    public async Task Idempotent_WhenAlreadyRegistered_NoAcceptedEvent_NoReplace()
    {
        var repo = new InMemoryTournamentRepository();
        var playerId = Guid.NewGuid();
        var t = new Tournament
        {
            MaxPlayers = 4,
            Status = TournamentStatus.Upcoming
        };
        t.Participants.Add(playerId);
        repo.Items.Add(t);

        var producer = new FakeKafkaProducerWrapper();
        var handler = new RegisterPlayerCommandHandler(repo, producer);

        var ok = await handler.Handle(new RegisterPlayerCommand(t.Id, playerId), CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(producer.Published.Where(p => p.Topic == "tournament.registration.accepted"));
        Assert.Null(repo.LastReplacedId);
        Assert.Single(t.Participants); // не дублируется
    }

    [Fact]
    public async Task Success_AddsPlayer_StoresAnd_PublishesAccepted()
    {
        var repo = new InMemoryTournamentRepository();
        var t = new Tournament
        {
            MaxPlayers = 2,
            Status = TournamentStatus.Upcoming
        };
        repo.Items.Add(t);

        var playerId = Guid.NewGuid();
        var producer = new FakeKafkaProducerWrapper();
        var handler = new RegisterPlayerCommandHandler(repo, producer);

        var ok = await handler.Handle(new RegisterPlayerCommand(t.Id, playerId), CancellationToken.None);

        Assert.True(ok);
        Assert.Contains(playerId, t.Participants);
        Assert.Equal(t.Id, repo.LastReplacedId);
        Assert.Single(producer.Published.Where(p => p.Topic == "tournament.registration.accepted"));
    }
}