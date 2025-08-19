using System;
using System.Linq;
using Xunit;
using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Commands;
using BattleService.GameLogic.Entities;

namespace BattleService.Tests;

public class GameLoopTests
{
    [Fact]
    public void RegisterPlayer_ThenMove_Tick_UpdatesPosition()
    {
        var loop = new GameLoop();
        var playerId = Guid.NewGuid();

        loop.RegisterPlayer(playerId);
        loop.Tick(0.05f); // применит EnsurePlayer

        Assert.True(loop.Snapshot.Ships.TryGetValue(playerId, out var ship));
        var startX = ship!.Position.X;

        loop.Enqueue(new MoveCommand(playerId, 1f)); // +тяга
        loop.Tick(1.0f); // v += 0.5, x += 0.5

        var endX = loop.Snapshot.Ships[playerId].Position.X;
        Assert.InRange(endX - startX, 0.49f, 0.51f);
    }

    [Fact]
    public void Projectile_Hit_AppliesDamage_RecordsHit_AndRemovesProjectile()
    {
        var loop = new GameLoop();
        var ctx = loop.Snapshot;

        var ship = new Ship(Guid.NewGuid(), new Vector2(0, 0));
        ctx.Ships[ship.Id] = ship;

        var proj = new Projectile(Guid.NewGuid(), new Vector2(0, 0), new Vector2(0, 0), damage: 10);
        ctx.Projectiles[proj.Id] = proj;

        loop.Tick(0.016f);

        Assert.False(ctx.Projectiles.ContainsKey(proj.Id));       // снаряд удалён
        Assert.Equal(90, ship.Hp);                                // урон применён
        Assert.Contains(ctx.Hits, h => h.ShipId == ship.Id && h.ProjectileId == proj.Id && Math.Abs(h.Damage - 10) < 0.001f);
    }

    [Fact]
    public void ShipShip_Collision_SeparatesShips()
    {
        var loop = new GameLoop();
        var ctx = loop.Snapshot;

        var a = new Ship(Guid.NewGuid(), new Vector2(0, 0));
        var b = new Ship(Guid.NewGuid(), new Vector2(30, 0)); // радиусы 18+18=36 => пересечение на 6
        ctx.Ships[a.Id] = a;
        ctx.Ships[b.Id] = b;

        loop.Tick(0.0f); // только коллизии/грид

        var dist = (b.Position - a.Position).Length();
        var sumR = a.Radius + b.Radius;
        Assert.True(dist >= sumR - 0.01f, $"Ships not separated enough: dist={dist}, expected >= {sumR}");
    }
}