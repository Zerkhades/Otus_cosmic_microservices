using System;
using Xunit;
using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Entities;
using BattleService.GameLogic.Commands;

namespace BattleService.Tests;

public class TurnCommandTests
{
    [Fact]
    public void Execute_RotatesShip_WhenShipExists()
    {
        var ctx = new GameContext();
        var ship = new Ship(Guid.NewGuid(), new Vector2(0, 0));
        ctx.Ships[ship.Id] = ship;

        var cmd = new TurnCommand(ship.Id, 45f);
        cmd.Execute(ctx);

        Assert.Equal(45f, ship.RotationDeg, 3);
    }

    [Fact]
    public void Execute_DoesNothing_WhenShipNotFound()
    {
        var ctx = new GameContext();
        var cmd = new TurnCommand(Guid.NewGuid(), 90f);

        cmd.Execute(ctx);

        Assert.Empty(ctx.Ships);
    }
}