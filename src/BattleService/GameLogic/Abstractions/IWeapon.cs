using BattleService.GameLogic.Entities;

namespace BattleService.GameLogic.Abstractions
{
    /// <summary>
    /// Базовый контракт вооружения.
    /// </summary>
    public interface IWeapon
    {
        string Code { get; }
        TimeSpan Cooldown { get; }
        DateTime LastShotAt { get; }
        bool CanShoot(DateTime now) => now - LastShotAt >= Cooldown;
        Projectile Shoot(DateTime now, Ship owner);
    }
}
