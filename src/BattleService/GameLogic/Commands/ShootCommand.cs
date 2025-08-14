using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Engine;

namespace BattleService.GameLogic.Commands
{
    public record ShootCommand(Guid ShipId, string WeaponCode) : ICommand
    {
        public void Execute(GameContext ctx)
        {
            if (!ctx.Ships.TryGetValue(ShipId, out var ship)) return;
            var weapon = ship.Weapons.FirstOrDefault(/*w => w.Code == WeaponCode*/);
            if (weapon is null) return;
            var proj = weapon.Shoot(DateTime.UtcNow, ship);
            ctx.Projectiles.Add(proj.Id, proj);
        }
    }
}
