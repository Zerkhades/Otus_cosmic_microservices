using BattleService.GameLogic.Entities;
using BattleService.GameLogic.Weapons.Strategies;

namespace BattleService.GameLogic.Weapons
{
    public class RocketLauncher : WeaponBase
    {
        public override string Code => "ROCKET";
        public override TimeSpan Cooldown => TimeSpan.FromSeconds(3);

        private static readonly IProjectileCreationStrategy FireStrategy =
            new ForwardProjectileStrategy(muzzleOffset: 40f, speed: 8f, damage: 40);

        protected override Projectile CreateProjectile(Ship owner) => FireStrategy.Create(owner);
    }
}