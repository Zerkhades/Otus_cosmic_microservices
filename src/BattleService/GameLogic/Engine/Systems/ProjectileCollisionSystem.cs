using BattleService.GameLogic.Entities;
using System;

namespace BattleService.GameLogic.Engine.Systems
{
    public sealed class ProjectileCollisionSystem : IGameSystem
    {
        private readonly SpatialHashGrid _grid;
        private readonly IProjectileHitHandler[] _handlers;

        public ProjectileCollisionSystem(SpatialHashGrid grid)
            : this(grid, Array.Empty<IProjectileHitHandler>())
        {
        }

        public ProjectileCollisionSystem(SpatialHashGrid grid, params IProjectileHitHandler[] handlers)
        {
            _grid = grid;
            _handlers = (handlers is { Length: > 0 })
                ? handlers
                : new IProjectileHitHandler[]
                {
                    new AddHitRecordHandler(),
                    new ApplyDamageHandler(),
                    new KnockbackHandler(20f),
                    new KillProjectileHandler()
                };
        }

        public void Update(GameContext ctx, float dt)
        {
            foreach (var p in ctx.Projectiles.Values)
            {
                if (!p.IsAlive) continue;

                foreach (var s in _grid.QueryShipsAround(p.Position))
                {
                    // при желании исключи самострелы:
                    // if (p.OwnerShipId == s.Id) continue;

                    var sumR = p.Radius + s.Radius;
                    if (Vector2.DistanceSquared(p.Position, s.Position) > sumR * sumR) continue;

                    // последовательно применяем обработчики попадания
                    foreach (var h in _handlers)
                    {
                        if (!h.Handle(ctx, p, s))
                            break;
                    }

                    // этот снаряд больше никого не заденет
                    break;
                }
            }
        }
    }

    public interface IProjectileHitHandler
    {
        /// <returns>true — продолжить цепочку; false — остановить</returns>
        bool Handle(GameContext ctx, Projectile p, Ship s);
    }

    public sealed class AddHitRecordHandler : IProjectileHitHandler
    {
        public bool Handle(GameContext ctx, Projectile p, Ship s)
        {
            ctx.Hits.Add((s.Id, p.Id, p.Damage));
            return true;
        }
    }

    public sealed class ApplyDamageHandler : IProjectileHitHandler
    {
        public bool Handle(GameContext ctx, Projectile p, Ship s)
        {
            s.Hp = Math.Max(0, s.Hp - p.Damage);
            return true;
        }
    }

    internal sealed class KnockbackHandler : IProjectileHitHandler
    {
        private readonly float _impulse;

        public KnockbackHandler(float impulse) => _impulse = impulse;

        public bool Handle(GameContext ctx, Projectile p, Ship s)
        {
            var delta = s.Position - p.Position;
            var dist = MathF.Sqrt(MathF.Max(delta.X * delta.X + delta.Y * delta.Y, 1e-6f));
            var n = new Vector2(delta.X / dist, delta.Y / dist);
            s.Velocity += n * _impulse;
            return true;
        }
    }

    public sealed class KillProjectileHandler : IProjectileHitHandler
    {
        public bool Handle(GameContext ctx, Projectile p, Ship s)
        {
            p.IsAlive = false;
            return false; // дальше нет смысла
        }
    }
}