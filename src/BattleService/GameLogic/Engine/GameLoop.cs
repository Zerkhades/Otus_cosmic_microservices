using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Commands;
using BattleService.GameLogic.Entities;
using System.Linq;

namespace BattleService.GameLogic.Engine
{
    public class GameContext
    {
        public Dictionary<Guid, Ship> Ships { get; } = new();
        public Dictionary<Guid, Projectile> Projectiles { get; } = new();
    }

    public class GameLoop
    {
        private readonly GameContext _ctx = new();
        private readonly Queue<ICommand> _queue = new();
        public GameContext Snapshot => _ctx;
        public void Enqueue(ICommand cmd) => _queue.Enqueue(cmd);

        public void Tick(float dt)
        {
            // 1) выполняем все команды, пришедшие за тик
            while (_queue.TryDequeue(out var cmd)) cmd.Execute(_ctx);

            // 2) обновляем физику (очень упрощённо)
            foreach (var m in _ctx.Ships.Values.Cast<IMovable>().Concat(_ctx.Projectiles.Values))
                UpdatePosition(m, dt);

            // 3) (TODO) коллизии, урон…
        }

        private static void UpdatePosition(IMovable obj, float dt)
        {
            var newPos = obj.Position + obj.Velocity * dt;
            typeof(IMovable).GetProperty("Position")!.SetValue(obj, newPos);
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
        public static Vector2 operator *(Vector2 a, float k) => new(a.X * k, a.Y * k);
        public static Vector2 ClampMagnitude(Vector2 v, float max)
        {
            var lenSq = v.X * v.X + v.Y * v.Y;
            if (lenSq <= max * max) return v;
            var factor = max / MathF.Sqrt(lenSq);
            return new Vector2(v.X * factor, v.Y * factor);
        }
    }
}
