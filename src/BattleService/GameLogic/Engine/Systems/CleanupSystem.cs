using System;
using System.Linq;

namespace BattleService.GameLogic.Engine.Systems
{
    internal sealed class CleanupSystem : IGameSystem
    {
        public void Update(GameContext ctx, float dt)
        {
            foreach (var dead in ctx.Projectiles.Values.Where(p => !p.IsAlive).Select(p => p.Id).ToArray())
                ctx.Projectiles.Remove(dead);

            foreach (var deadShip in ctx.Ships.Values.Where(s => !s.IsAlive).Select(s => s.Id).ToArray())
                ctx.Ships.Remove(deadShip);
        }
    }
}