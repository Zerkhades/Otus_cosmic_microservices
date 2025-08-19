using System.Linq;
using BattleService.GameLogic.Abstractions;

namespace BattleService.GameLogic.Engine.Systems
{
    internal sealed class MovementSystem : IGameSystem
    {
        public void Update(GameContext ctx, float dt)
        {
            foreach (var m in ctx.Ships.Values.Cast<IMovable>().Concat(ctx.Projectiles.Values))
            {
                var newPos = m.Position + m.Velocity * dt;
                m.Position = newPos;
            }
        }
    }
}