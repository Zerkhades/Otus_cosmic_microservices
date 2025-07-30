using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Entities;

namespace BattleService.GameLogic.Weapons
{
    public class RocketLauncher : WeaponBase
    {
        public override string Code => "ROCKET";
        public override TimeSpan Cooldown => TimeSpan.FromSeconds(3);
        protected override Projectile CreateProjectile(Ship owner)
        {
            var rad = owner.RotationDeg * MathF.PI / 180f;
            var dir = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
            var pos = owner.Position + dir * 2f;
            var vel = dir * 8f;
            return new Projectile(owner.Id, pos, vel, damage: 40);
        }
    }
}
