using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Entities;

namespace BattleService.GameLogic.Weapons
{
    public abstract class WeaponBase : IWeapon
    {
        public abstract string Code { get; }
        public abstract TimeSpan Cooldown { get; }
        public DateTime LastShotAt { get; private set; } = DateTime.MinValue;

        public Projectile Shoot(DateTime now, Ship owner)
        {
            if (!CanShoot(now)) throw new InvalidOperationException("Weapon cooldown");
            LastShotAt = now;
            return CreateProjectile(owner);
        }

        public bool CanShoot(DateTime now) => now - LastShotAt >= Cooldown;
        

        protected abstract Projectile CreateProjectile(Ship owner);
    }
}
