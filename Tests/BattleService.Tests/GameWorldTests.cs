using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Logging;
using BattleService.Application.Worlds;
using BattleService.GameLogic.Engine;
using Grpc.Core;
using BattleService.Protos;

namespace BattleService.Tests;

public class GameWorldTests
{
    private sealed class TestGameLoopFactory : IGameLoopFactory
    {
        public GameLoop Instance { get; } = new();
        public GameLoop Create(Guid battleId) => Instance;
    }

    private sealed class DummyStreamWriter : IServerStreamWriter<ServerUpdate>
    {
        public WriteOptions? WriteOptions { get; set; }
        public Task WriteAsync(ServerUpdate message) => Task.CompletedTask;
    }

    private static ILogger CreateLogger() =>
        LoggerFactory.Create(b => { }).CreateLogger("test");

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
    public async Task AcceptPayload_TURN_Command_AppliesRotation()
    {
        var loopFactory = new TestGameLoopFactory();
        var world = new GameWorld(Guid.NewGuid(), loopFactory, CreateLogger());
        world.Start();

        var playerId = Guid.NewGuid().ToString();
        var conn = world.AddConnection(new DummyStreamWriter(), playerId);
        world.EnsurePlayer(playerId);

        // дождаться появления корабля
        Assert.True(await WaitUntilAsync(
            () => loopFactory.Instance.Snapshot.Ships.Count == 1, TimeSpan.FromSeconds(1)));

        // Новый формат payload: один TURN со значением +1 => 180*0.05 = 9 градусов
        var json = JsonSerializer.SerializeToUtf8Bytes(new
        {
            kind = "cmd",
            tick = 1,
            commands = new[] { new { type = "TURN", value = 1 } }
        });

        world.AcceptPayload(playerId, json);

        // дождаться применения команды
        Assert.True(await WaitUntilAsync(
            () =>
            {
                var ship = loopFactory.Instance.Snapshot.Ships.Values.FirstOrDefault();
                return ship is not null && Math.Abs(ship.RotationDeg - 9f) < 0.001f;
            },
            TimeSpan.FromSeconds(1)));

        await world.StopAsync();
        world.RemoveConnection(conn);
    }
}