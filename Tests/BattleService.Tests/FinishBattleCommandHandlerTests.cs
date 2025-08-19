using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using NSubstitute;
using MediatR;
using Microsoft.Extensions.Logging;
using BattleService.Infrastructure.State;
using BattleService.Application.Commands;
using BattleService.Domains;
using BattleService.Application.Events;
using BattleService.Infrastructure.Kafka;

namespace BattleService.Tests;

public class FinishBattleCommandHandlerTests
{
    [Fact]
    public async Task ReturnsFalse_WhenBattleNotFound()
    {
        var store = new InMemoryBattleStore();
        var producer = Substitute.For<IKafkaProducerWrapper>();
        var logger = Substitute.For<ILogger<FinishBattleCommandHandler>>();
        var mediator = Substitute.For<IMediator>();

        var handler = new FinishBattleCommandHandler(store, producer, logger, mediator);
        var result = await handler.Handle(new FinishBattleCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.False(result);
        await mediator.DidNotReceiveWithAnyArgs().Publish(default(BattleFinishedDomainEvent)!, default);
        await producer.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, default);
    }

    [Fact]
    public async Task Idempotent_WhenAlreadyFinished_NoKafkaNoEvent()
    {
        var store = new InMemoryBattleStore();
        var battle = new Battle { TournamentId = Guid.NewGuid() };
        battle.Finish();
        store.Add(battle);

        var producer = Substitute.For<IKafkaProducerWrapper>();
        var logger = Substitute.For<ILogger<FinishBattleCommandHandler>>();
        var mediator = Substitute.For<IMediator>();

        var handler = new FinishBattleCommandHandler(store, producer, logger, mediator);
        var result = await handler.Handle(new FinishBattleCommand(battle.Id), CancellationToken.None);

        Assert.True(result);
        await mediator.DidNotReceiveWithAnyArgs().Publish(default(BattleFinishedDomainEvent)!, default);
        await producer.DidNotReceiveWithAnyArgs().PublishAsync(default!, default!, default);
    }

    [Fact]
    public async Task Success_PublishesDomainEvent_AndKafka_AndFinishesBattle()
    {
        var store = new InMemoryBattleStore();
        var battle = new Battle { TournamentId = Guid.NewGuid() };
        battle.Participants.Add(Guid.NewGuid());
        battle.Start();
        store.Add(battle);

        var producer = Substitute.For<IKafkaProducerWrapper>();
        var logger = Substitute.For<ILogger<FinishBattleCommandHandler>>();
        var mediator = Substitute.For<IMediator>();

        var handler = new FinishBattleCommandHandler(store, producer, logger, mediator);
        var result = await handler.Handle(new FinishBattleCommand(battle.Id), CancellationToken.None);

        Assert.True(result);
        Assert.Equal(BattleStatus.Finished, store.TryGet(battle.Id, out var b) && b != null ? b.Status : BattleStatus.Waiting);
        await mediator.Received(1).Publish(Arg.Any<BattleFinishedDomainEvent>(), Arg.Any<CancellationToken>());
        await producer.Received(1).PublishAsync("battle.finished", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task KafkaFailure_ReturnsFalse_ButBattleIsFinished()
    {
        var store = new InMemoryBattleStore();
        var battle = new Battle { TournamentId = Guid.NewGuid() };
        battle.Start();
        store.Add(battle);

        var producer = Substitute.For<IKafkaProducerWrapper>();
        producer.PublishAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Exception("Kafka down"));

        var logger = Substitute.For<ILogger<FinishBattleCommandHandler>>();
        var mediator = Substitute.For<IMediator>();

        var handler = new FinishBattleCommandHandler(store, producer, logger, mediator);
        var result = await handler.Handle(new FinishBattleCommand(battle.Id), CancellationToken.None);

        Assert.False(result);
        Assert.Equal(BattleStatus.Finished, store.TryGet(battle.Id, out var b) && b != null ? b.Status : BattleStatus.Waiting);
        await mediator.Received(1).Publish(Arg.Any<BattleFinishedDomainEvent>(), Arg.Any<CancellationToken>());
    }
}