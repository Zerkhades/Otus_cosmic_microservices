using BattleService.GameLogic.Entities;

namespace BattleService.GameLogic.Weapons.Strategies
{
    public interface IProjectileCreationStrategy
    {
        Projectile Create(Ship owner);
    }
}
