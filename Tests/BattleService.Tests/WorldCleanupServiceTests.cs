using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BattleService.Application.Worlds;
using BattleService.GameLogic.Engine;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BattleService.Tests;

public class WorldCleanupServiceTests
{
    private sealed class TestGameLoopFactory : IGameLoopFactory
    {
        public GameLoop Create(Guid battleId) => new GameLoop();
    }

    private static ILogger<T> CreateLogger<T>() =>
        LoggerFactory.Create(b => { }).CreateLogger<T>();

    private static void SetLastActivityUtc(GameWorld world, DateTime value)
    {
        // У авто‑свойства LastActivityUtc приватный setter → меняем backing field через reflection.
        var fi = typeof(GameWorld).GetField("<LastActivityUtc>k__BackingField",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(fi);
        fi!.SetValue(world, value);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> cond, TimeSpan timeout)
    {
        var stop = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < stop)
        {
            if (cond()) return true;
            await Task.Delay(10);
        }
        return false;
    }

    [Fact]
    public async Task Removes_World_WhenIdleAndNoClients()
    {
        var mgr = new BattleWorldManager(new TestGameLoopFactory(), CreateLogger<BattleWorldManager>());
        var worldId = Guid.NewGuid();
        var world = mgr.GetOrCreate(worldId);

        // Сделать мир «простойшим» дольше порога и без клиентов
        SetLastActivityUtc(world, DateTime.UtcNow.AddMinutes(-5));

        var svc = new WorldCleanupService(mgr, CreateLogger<WorldCleanupService>());
        await svc.StartAsync(default);

        // Ждём пока сервис уберёт мир (первый цикл выполняется сразу, задержка отменяется при остановке)
        Assert.True(await WaitUntilAsync(
            () => !mgr.Worlds.Any(w => w.BattleId == worldId),
            TimeSpan.FromSeconds(2)));

        await svc.StopAsync(default);
    }
}