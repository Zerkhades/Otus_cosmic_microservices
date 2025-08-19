using System;
using Xunit;
using BattleService.GameLogic.Weapons;
using BattleService.GameLogic.Entities;
using BattleService.GameLogic.Engine;

namespace BattleService.Tests;

public class WeaponTests
{
    [Fact]
    public void Laser_Cooldown_PreventsSpam()
    {
        var weapon = new LaserWeapon();
        var ship = new Ship(Guid.NewGuid(), new Vector2(0, 0));
        var t0 = DateTime.UtcNow;

        var p1 = weapon.Shoot(t0, ship);
        Assert.NotNull(p1);

        Assert.Throws<InvalidOperationException>(() => weapon.Shoot(t0.AddMilliseconds(100), ship));
        var p2 = weapon.Shoot(t0.AddMilliseconds(210), ship);
        Assert.NotNull(p2);
    }
}