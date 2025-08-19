using System;
using System.Collections.Generic;

namespace BattleService.GameLogic.Engine.Systems
{
    internal sealed class ShipCollisionSystem : IGameSystem
    {
        private readonly SpatialHashGrid _grid;
        private readonly float _restitution;
        private readonly float _damping;

        public ShipCollisionSystem(SpatialHashGrid grid, float restitution, float damping)
        {
            _grid = grid;
            _restitution = restitution;
            _damping = damping;
        }

        public void Update(GameContext ctx, float dt)
        {
            // чтобы не обрабатывать пары дважды
            var seen = new HashSet<(Guid, Guid)>();

            foreach (var a in ctx.Ships.Values)
            {
                foreach (var b in _grid.QueryShipsAround(a.Position))
                {
                    if (a.Id == b.Id) continue;

                    // нормализованный «ключ пары», чтобы (a,b) == (b,a)
                    var key = a.Id.CompareTo(b.Id) < 0 ? (a.Id, b.Id) : (b.Id, a.Id);
                    if (!seen.Add(key)) continue;

                    var sumR = a.Radius + b.Radius;
                    var distSq = Vector2.DistanceSquared(a.Position, b.Position);
                    if (distSq > sumR * sumR) continue;

                    var dist = MathF.Sqrt(MathF.Max(distSq, 1e-6f));
                    var n = (b.Position - a.Position) * (1f / dist);

                    var penetration = sumR - dist;
                    var corr = n * (penetration * 0.5f);

                    a.Position -= corr;
                    b.Position += corr;

                    a.Velocity *= _damping;
                    b.Velocity *= _damping;

                    // небольшая упругая составляющая по нормали
                    var vaN = a.Velocity.X * n.X + a.Velocity.Y * n.Y;
                    var vbN = b.Velocity.X * n.X + b.Velocity.Y * n.Y;
                    var impulse = (vbN - vaN) * _restitution;
                    a.Velocity += n * impulse;
                    b.Velocity -= n * impulse;
                }
            }
        }
    }
}