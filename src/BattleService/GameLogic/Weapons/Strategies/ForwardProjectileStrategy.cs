using BattleService.GameLogic.Engine;
using BattleService.GameLogic.Entities;

namespace BattleService.GameLogic.Weapons.Strategies
{
    public sealed class ForwardProjectileStrategy : IProjectileCreationStrategy
    {
        private readonly float _muzzleOffset;
        private readonly float _speed;
        private readonly int _damage;

        public ForwardProjectileStrategy(float muzzleOffset, float speed, int damage)
        {
            _muzzleOffset = muzzleOffset;
            _speed = speed;
            _damage = damage;
        }

        public Projectile Create(Ship owner)
        {
            var rad = owner.RotationDeg * MathF.PI / 180f;
            var dir = new Vector2(MathF.Cos(rad), MathF.Sin(rad));
            var pos = owner.Position + dir * _muzzleOffset;
            var vel = dir * _speed;
            return new Projectile(owner.Id, pos, vel, damage: _damage);
        }
    }
}