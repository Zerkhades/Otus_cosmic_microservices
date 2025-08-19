using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Engine;

namespace BattleService.GameLogic.Entities
{
    /// <summary>Корабль игрока.</summary>
    public class Ship : IMovable
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid PlayerId { get; init; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; set; }
        public float Radius { get; init; } = 18f;
        public float Mass { get; init; } = 1f;
        public float RotationDeg { get; private set; }
        public float MaxHp { get; init; } = 100;
        public float Hp { get; set; } = 100;
        public bool IsAlive => Hp > 0;

        private readonly List<IWeapon> _weapons = new();

        public Ship(Guid playerId, Vector2 spawnPos)
        {
            PlayerId = playerId;
            Position = spawnPos;
            RotationDeg = 0;
        }

        #region IMovable
        private const float MaxSpeed = 30f;
        private const float ThrustPower = 3f;

        public void ApplyThrust(float delta)
        {
            var rad = RotationDeg * MathF.PI / 180f;
            var direction = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
            Velocity += direction * (ThrustPower * delta);
            Velocity = Vector2.ClampMagnitude(Velocity, MaxSpeed);
        }

        public void Rotate(float degDelta) => RotationDeg = (RotationDeg + degDelta + 360) % 360;
        #endregion

        public IEnumerable<IWeapon> Weapons => _weapons;
        public void Equip(IWeapon weapon) => _weapons.Add(weapon);

        public void Move(Vector2 delta)
        {
            throw new NotImplementedException();
        }
    }
}
