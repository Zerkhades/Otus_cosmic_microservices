using System;
using System.Linq;
using Xunit;
using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Entities;
using BattleService.GameLogic.Commands;
using BattleService.GameLogic.Weapons;

namespace BattleService.Tests;

public class ShootCommandTests
{
    [Fact]
    public void Execute_AddsProjectile_WhenWeaponAvailable()
    {
        var ctx = new GameContext();
        var ship = new Ship(Guid.NewGuid(), new Vector2(0, 0));
        ship.Equip(new LaserWeapon()); // FirstOrDefault() найдёт его
        ctx.Ships[ship.Id] = ship;

        var cmd = new ShootCommand(ship.Id, "LASER");
        cmd.Execute(ctx);

        Assert.Single(ctx.Projectiles);
        var p = ctx.Projectiles.Values.First();
        Assert.Equal(ship.Id, p.OwnerShipId);
        Assert.True(p.IsAlive);
    }

    [Fact]
    public void Execute_DoesNothing_WhenNoWeapons()
    {
        var ctx = new GameContext();
        var ship = new Ship(Guid.NewGuid(), new Vector2(0, 0));
        ctx.Ships[ship.Id] = ship;

        var cmd = new ShootCommand(ship.Id, "ANY");
        cmd.Execute(ctx);

        Assert.Empty(ctx.Projectiles);
    }

    [Fact]
    public void Execute_DoesNothing_WhenShipNotFound()
    {
        var ctx = new GameContext();

        var cmd = new ShootCommand(Guid.NewGuid(), "LASER");
        cmd.Execute(ctx);

        Assert.Empty(ctx.Projectiles);
    }
}