using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Commands;
using BattleService.GameLogic.Entities;
using System;
using System.Linq;

namespace BattleService.GameLogic.Engine
{
    public class GameContext
    {
        public Dictionary<Guid, Ship> Ships { get; } = new();
        public Dictionary<Guid, Projectile> Projectiles { get; } = new();
        public List<(Guid ShipId, Guid ProjectileId, float Damage)> Hits { get; } = new();
    }

    public class GameLoop
    {
        private readonly GameContext _ctx = new();
        private readonly Queue<ICommand> _queue = new();
        public GameContext Snapshot => _ctx;
        private readonly SpatialHashGrid _grid = new(cellSize: 64f);
        public void Enqueue(ICommand cmd) => _queue.Enqueue(cmd);

        public void Tick(float dt)
        {
            _ctx.Hits.Clear();

            // 1) команды
            while (_queue.TryDequeue(out var cmd)) cmd.Execute(_ctx);

            // 2) физика
            foreach (var m in _ctx.Ships.Values.Cast<IMovable>().Concat(_ctx.Projectiles.Values))
                UpdatePosition(m, dt);

            // 3) перестроить grid
            _grid.Clear();
            foreach (var s in _ctx.Ships.Values) _grid.Insert(s);
            foreach (var p in _ctx.Projectiles.Values.Where(p => p.IsAlive)) _grid.Insert(p);

            // 4) коллизии
            ResolveShipShipGrid(_ctx, _grid, restitution: 0.2f, damping: 0.6f);
            ResolveProjectileShipGrid(_ctx, _grid);

            // 5) зачистка «мертвых»
            foreach (var dead in _ctx.Projectiles.Values.Where(p => !p.IsAlive).Select(p => p.Id).ToArray())
                _ctx.Projectiles.Remove(dead);

            foreach (var deadShip in _ctx.Ships.Values.Where(s => !s.IsAlive).Select(s => s.Id).ToArray())
                _ctx.Ships.Remove(deadShip);
        }

        private static void UpdatePosition(IMovable obj, float dt)
        {
            var newPos = obj.Position + obj.Velocity * dt;
            obj.Position = newPos;
        }
        /// <summary>
        /// Гарантирует, что в мире есть корабль игрока. Потокобезопасно:
        /// просто кладёт команду в очередь, обработка — в тике.
        /// </summary>
        public void RegisterPlayer(Guid playerId)
        {
            Enqueue(new EnsurePlayerCommand(playerId));
        }
        private static void ResolveShipShipGrid(GameContext ctx, SpatialHashGrid grid, float restitution, float damping)
        {
            // чтобы не обрабатывать пары дважды
            var seen = new HashSet<(Guid, Guid)>();

            foreach (var a in ctx.Ships.Values)
            {
                foreach (var b in grid.QueryShipsAround(a.Position))
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

                    a.Velocity *= damping;
                    b.Velocity *= damping;

                    // небольшая упругая составляющая по нормали
                    var vaN = a.Velocity.X * n.X + a.Velocity.Y * n.Y;
                    var vbN = b.Velocity.X * n.X + b.Velocity.Y * n.Y;
                    var impulse = (vbN - vaN) * restitution;
                    a.Velocity += n * impulse;
                    b.Velocity -= n * impulse;
                }
            }
        }

        private static void ResolveProjectileShipGrid(GameContext ctx, SpatialHashGrid grid)
        {
            foreach (var p in ctx.Projectiles.Values)
            {
                if (!p.IsAlive) continue;

                foreach (var s in grid.QueryShipsAround(p.Position))
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

    // =====================================
    //  Vector2 helper (minimal, вместо System.Numerics для примера)
    // =====================================
    public readonly record struct Vector2(float X, float Y)
    {
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.X + b.X, a.Y + b.Y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.X - b.X, a.Y - b.Y);
        public static Vector2 operator *(Vector2 a, float k) => new(a.X * k, a.Y * k);

        public float Length() => MathF.Sqrt(X * X + Y * Y);
        public float SqrLength() => X * X + Y * Y;

        public Vector2 Normalized()
        {
            var len = Length();
            return len > 1e-6f ? new Vector2(X / len, Y / len) : new Vector2(0, 0);
        }

        public static float DistanceSquared(Vector2 a, Vector2 b)
        {
            var dx = a.X - b.X; var dy = a.Y - b.Y;
            return dx * dx + dy * dy;
        }

        public static Vector2 ClampMagnitude(Vector2 v, float max)
        {
            var lenSq = v.X * v.X + v.Y * v.Y;
            if (lenSq <= max * max) return v;
            var factor = max / MathF.Sqrt(lenSq);
            return new Vector2(v.X * factor, v.Y * factor);
        }
    }
}
