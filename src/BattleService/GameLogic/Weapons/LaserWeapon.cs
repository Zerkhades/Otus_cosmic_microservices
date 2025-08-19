using BattleService.GameLogic.Entities;
using BattleService.GameLogic.Weapons.Strategies;

namespace BattleService.GameLogic.Weapons
{
    public class LaserWeapon : WeaponBase
    {
        public override string Code => "LASER";
        public override TimeSpan Cooldown => TimeSpan.FromMilliseconds(200);

        private static readonly IProjectileCreationStrategy FireStrategy =
            new ForwardProjectileStrategy(muzzleOffset: 1.5f, speed: 20f, damage: 10);

        protected override Projectile CreateProjectile(Ship owner) => FireStrategy.Create(owner);
    }
}