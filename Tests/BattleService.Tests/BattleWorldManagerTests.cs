using System;
using Xunit;
using Microsoft.Extensions.Logging;
using BattleService.Application.Worlds;

namespace BattleService.Tests;

public class BattleWorldManagerTests
{
    private sealed class DummyFactory : IGameLoopFactory
    {
        public GameLogic.Engine.GameLoop Create(Guid battleId) => new();
    }

    private static ILogger<T> L<T>() =>
        LoggerFactory.Create(b => { }).CreateLogger<T>();

    [Fact]
    public void GetOrCreate_ReturnsSameInstance_ForSameId()
    {
        var mgr = new BattleWorldManager(new DummyFactory(), L<BattleWorldManager>());
        var id = Guid.NewGuid();

        var w1 = mgr.GetOrCreate(id);
        var w2 = mgr.GetOrCreate(id);

        Assert.Same(w1, w2);
    }

    [Fact]
    public void Remove_RemovesWorld()
    {
        var mgr = new BattleWorldManager(new DummyFactory(), L<BattleWorldManager>());
        var id = Guid.NewGuid();

        var w1 = mgr.GetOrCreate(id);
        mgr.Remove(id);

        var w2 = mgr.GetOrCreate(id);
        Assert.NotSame(w1, w2);
    }
}