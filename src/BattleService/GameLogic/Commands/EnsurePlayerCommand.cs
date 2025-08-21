using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Entities;
using BattleService.GameLogic.Weapons;
using System;

namespace BattleService.GameLogic.Commands
{
    public sealed class EnsurePlayerCommand : ICommand
    {
        //private readonly IWeaponFactory _weapons;
        public EnsurePlayerCommand(Guid playerId)
        {
            PlayerId = playerId;
        }

        public Guid PlayerId { get; }

        public void Execute(GameContext ctx)
        {
            // 1) Уже есть — выходим (идемпотентность)
            if (ctx.Ships.ContainsKey(PlayerId))
                return;

            // 2) Вычислим детерминированную точку спауна по GUID,
            //    чтобы игроки не появлялись в одном месте.
            var (x, y, angle) = GetSpawnPoint(
                PlayerId,
                100,
                100);

            // 3) Создаём корабль со стартовым состоянием
            var ship = new Ship(PlayerId, new Vector2(x, y));
            ship.Equip(new RocketLauncher()); // пример оружия

            ctx.Ships.Add(PlayerId, ship);
        }

        private static (float x, float y, float angle) GetSpawnPoint(Guid id, float width, float height)
        {
            int h = 17;
            foreach (var b in id.ToByteArray())
                unchecked { h = h * 31 + b; }

            var t = (h & 0x7FFFFFFF) / (float)int.MaxValue;

            // спаун по окружности вокруг центра
            var cx = width * 0.5f;
            var cy = height * 0.5f;
            var r = MathF.Min(width, height) * 0.35f;
            var ang = t * MathF.PI * 2;

            var x = cx + MathF.Cos(ang) * r;
            var y = cy + MathF.Sin(ang) * r;

            // направим нос в центр
            var lookToCenter = MathF.Atan2(cy - y, cx - x);
            return (x, y, lookToCenter);
        }
    }
}
