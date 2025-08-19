using System;
using Xunit;
using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Entities;
using BattleService.GameLogic.Commands;

namespace BattleService.Tests;

public class MoveCommandTests
{
    [Fact]
    public void Execute_AppliesThrust_AlongShipForward()
    {
        var ctx = new GameContext();
        var ship = new Ship(Guid.NewGuid(), new Vector2(0, 0));
        // По умолчанию RotationDeg = 0, тяга вперёд по оси X
        ctx.Ships[ship.Id] = ship;

        var cmd = new MoveCommand(ship.Id, 1f);
        cmd.Execute(ctx);

        // ThrustPower = 0.5f => скорость (0.5, 0)
        Assert.Equal(3f, ship.Velocity.X, 3);
        Assert.Equal(0f, ship.Velocity.Y, 3);
    }

    [Fact]
    public void Execute_DoesNothing_WhenShipNotFound()
    {
        var ctx = new GameContext();

        var cmd = new MoveCommand(Guid.NewGuid(), 1f);
        cmd.Execute(ctx);

        Assert.Empty(ctx.Ships);
        Assert.Empty(ctx.Projectiles);
    }
}