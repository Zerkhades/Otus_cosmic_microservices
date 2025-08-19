using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Commands;
using BattleService.GameLogic.Entities;
using System;
using System.Linq;
using BattleService.GameLogic.Engine.Systems;

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
        private readonly SpatialHashGrid _grid;
        private readonly IGameSystem[] _systems;

        public GameContext Snapshot => _ctx;

        public GameLoop()
            : this(new SpatialHashGrid(cellSize: 64f), null)
        {
        }

        internal GameLoop(SpatialHashGrid grid, IGameSystem[]? systems = null)
        {
            _grid = grid;

            // Пайплайн по умолчанию
            _systems = systems ?? new IGameSystem[]
            {
                new MovementSystem(),
                new SpatialIndexSystem(_grid),
                new ShipCollisionSystem(_grid, restitution: 0.2f, damping: 0.6f),
                new ProjectileCollisionSystem(_grid),
                new CleanupSystem()
            };
        }

        public void Enqueue(ICommand cmd) => _queue.Enqueue(cmd);

        public void Tick(float dt)
        {
            _ctx.Hits.Clear();

            // 1) команды
            while (_queue.TryDequeue(out var cmd)) cmd.Execute(_ctx);

            // 2..N) системы симуляции
            foreach (var system in _systems)
                system.Update(_ctx, dt);
        }

        /// <summary>
        /// Гарантирует, что в мире есть корабль игрока. Потокобезопасно:
        /// просто кладёт команду в очередь, обработка — в тике.
        /// </summary>
        public void RegisterPlayer(Guid playerId)
        {
            Enqueue(new EnsurePlayerCommand(playerId));
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