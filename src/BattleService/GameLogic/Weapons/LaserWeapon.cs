using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Entities;

namespace BattleService.GameLogic.Weapons
{
    public class LaserWeapon : WeaponBase
    {
        public override string Code => "LASER";
        public override TimeSpan Cooldown => TimeSpan.FromMilliseconds(200);

        protected override Projectile CreateProjectile(Ship owner)
        {
            var rad = owner.RotationDeg * MathF.PI / 180f;
            var dir = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
            var pos = owner.Position + dir * 1.5f;  // нос корабля
            var vel = dir * 20f;                    // скорость лазера
            return new Projectile(owner.Id, pos, vel, damage: 10);
        }
    }
}
