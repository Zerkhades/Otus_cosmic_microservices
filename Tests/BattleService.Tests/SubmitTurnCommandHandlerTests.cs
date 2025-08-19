using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using BattleService.Infrastructure.State;
using BattleService.Application.Commands;
using BattleService.Domains;

namespace BattleService.Tests;

public class SubmitTurnCommandHandlerTests
{
    [Fact]
    public async Task Throws_WhenBattleNotFound()
    {
        var store = new InMemoryBattleStore();
        var logger = Substitute.For<ILogger<SubmitTurnCommandHandler>>();
        var handler = new SubmitTurnCommandHandler(store, logger);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new SubmitTurnCommand(Guid.NewGuid(), "p", 1, new byte[] { 1, 2, 3 }), CancellationToken.None));
    }

    [Fact]
    public async Task Throws_WhenBattleNotRunning()
    {
        var store = new InMemoryBattleStore();
        var b = new Battle();
        store.Add(b);

        var logger = Substitute.For<ILogger<SubmitTurnCommandHandler>>();
        var handler = new SubmitTurnCommandHandler(store, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SubmitTurnCommand(b.Id, "p", 1, Array.Empty<byte>()), CancellationToken.None));
    }

    [Fact]
    public async Task UpdatesCurrentTick_WhenValid()
    {
        var store = new InMemoryBattleStore();
        var b = new Battle(); b.Start();
        store.Add(b);

        var logger = Substitute.For<ILogger<SubmitTurnCommandHandler>>();
        var handler = new SubmitTurnCommandHandler(store, logger);

        await handler.Handle(new SubmitTurnCommand(b.Id, "p", 42, new byte[] { 9 }), CancellationToken.None);

        Assert.True(store.TryGet(b.Id, out var reloaded) && reloaded!.CurrentTick == 42);
    }
}