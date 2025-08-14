using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Engine;


namespace BattleService.GameLogic.Entities
{
    /// <summary>Снаряд, летящий по трассе.</summary>
    public class Projectile : IMovable
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public Guid OwnerShipId { get; init; }
        public Vector2 Position { get; set; }
        public Vector2 Velocity { get; private set; }
        public float RotationDeg { get; private set; }
        public float Damage { get; init; }

        public Projectile(Guid owner, Vector2 pos, Vector2 vel, float damage)
        {
            OwnerShipId = owner;
            Position = pos;
            Velocity = vel;
            Damage = damage;
        }

        public void ApplyThrust(float delta) { /* projectiles не разгоняются */ }
        public void Rotate(float degDelta) { }
    }
}
