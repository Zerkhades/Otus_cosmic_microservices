using System;
using Moq;
using Xunit;
using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Engine.Systems;
using BattleService.GameLogic.Entities;

namespace BattleService.Tests;

public class ProjectileCollisionSystemTests
{
    [Fact]
    public void Handlers_AreCalled_InOrder_And_AllowsContinuation()
    {
        var grid = new SpatialHashGrid(64f);
        var ctx = new GameContext();

        var ship = new Ship(Guid.NewGuid(), new Vector2(1f, 0f));
        ctx.Ships[ship.Id] = ship;
        grid.Insert(ship);

        var proj = new Projectile(Guid.NewGuid(), new Vector2(0f, 0f), new Vector2(0f, 0f), damage: 5f);
        ctx.Projectiles[proj.Id] = proj;

        var seq = new MockSequence();
        var h1 = new Mock<IProjectileHitHandler>(MockBehavior.Strict);
        var h2 = new Mock<IProjectileHitHandler>(MockBehavior.Strict);

        h1.InSequence(seq)
          .Setup(h => h.Handle(ctx, proj, ship))
          .Returns(true);
        h2.InSequence(seq)
          .Setup(h => h.Handle(ctx, proj, ship))
          .Returns(true);

        var system = new ProjectileCollisionSystem(grid, h1.Object, h2.Object);

        system.Update(ctx, 0.016f);

        h1.Verify(h => h.Handle(ctx, proj, ship), Times.Once);
        h2.Verify(h => h.Handle(ctx, proj, ship), Times.Once);
    }

    [Fact]
    public void Chain_Stops_WhenHandlerReturnsFalse_NextHandlersNotCalled()
    {
        var grid = new SpatialHashGrid(64f);
        var ctx = new GameContext();

        var ship = new Ship(Guid.NewGuid(), new Vector2(1f, 0f));
        ctx.Ships[ship.Id] = ship;
        grid.Insert(ship);

        var proj = new Projectile(Guid.NewGuid(), new Vector2(0f, 0f), new Vector2(0f, 0f), damage: 5f);
        ctx.Projectiles[proj.Id] = proj;

        var h1 = new Mock<IProjectileHitHandler>(MockBehavior.Strict);
        var h2 = new Mock<IProjectileHitHandler>(MockBehavior.Strict);

        h1.Setup(h => h.Handle(ctx, proj, ship)).Returns(false);

        var system = new ProjectileCollisionSystem(grid, h1.Object, h2.Object);

        system.Update(ctx, 0.016f);

        h1.Verify(h => h.Handle(ctx, proj, ship), Times.Once);
        h2.Verify(h => h.Handle(It.IsAny<GameContext>(), It.IsAny<Projectile>(), It.IsAny<Ship>()), Times.Never);
    }

    [Fact]
    public void CustomChain_WithoutKill_LeavesProjectileAlive()
    {
        var grid = new SpatialHashGrid(64f);
        var ctx = new GameContext();

        var ship = new Ship(Guid.NewGuid(), new Vector2(1f, 0f));
        ctx.Ships[ship.Id] = ship;
        grid.Insert(ship);

        var proj = new Projectile(Guid.NewGuid(), new Vector2(0f, 0f), new Vector2(0f, 0f), damage: 5f);
        ctx.Projectiles[proj.Id] = proj;

        var h1 = new Mock<IProjectileHitHandler>();
        var h2 = new Mock<IProjectileHitHandler>();

        h1.Setup(h => h.Handle(ctx, proj, ship)).Returns(true);
        h2.Setup(h => h.Handle(ctx, proj, ship)).Returns(true);

        var system = new ProjectileCollisionSystem(grid, h1.Object, h2.Object);

        system.Update(ctx, 0.016f);

        Assert.True(proj.IsAlive); // нет KillProjectileHandler — снаряд живой
        h1.Verify(h => h.Handle(ctx, proj, ship), Times.Once);
        h2.Verify(h => h.Handle(ctx, proj, ship), Times.Once);
    }
}