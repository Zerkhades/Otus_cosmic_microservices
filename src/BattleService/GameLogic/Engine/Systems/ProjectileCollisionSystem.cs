using System;

namespace BattleService.GameLogic.Engine.Systems
{
    internal sealed class ProjectileCollisionSystem : IGameSystem
    {
        private readonly SpatialHashGrid _grid;

        public ProjectileCollisionSystem(SpatialHashGrid grid) => _grid = grid;

        public void Update(GameContext ctx, float dt)
        {
            foreach (var p in ctx.Projectiles.Values)
            {
                if (!p.IsAlive) continue;

                foreach (var s in _grid.QueryShipsAround(p.Position))
                {
                    // при желании исключи самострелы:
                    // if (p.OwnerId == s.Id) continue;

                    var sumR = p.Radius + s.Radius;
                    if (Vector2.DistanceSquared(p.Position, s.Position) > sumR * sumR) continue;

                    // попадание
                    ctx.Hits.Add((s.Id, p.Id, p.Damage));
                    p.IsAlive = false;

                    // применяем урон
                    s.Hp = Math.Max(0, s.Hp - p.Damage);

                    // лёгкий «тычок» по нормали — визуальный отклик
                    var delta = s.Position - p.Position;
                    var dist = MathF.Sqrt(MathF.Max(delta.X * delta.X + delta.Y * delta.Y, 1e-6f));
                    var n = new Vector2(delta.X / dist, delta.Y / dist);
                    s.Velocity += n * 20f;

                    // этот снаряд больше никого не заденет
                    break;
                }
            }
        }
    }
}