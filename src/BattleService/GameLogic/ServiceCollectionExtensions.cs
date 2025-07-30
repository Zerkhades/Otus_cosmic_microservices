using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Weapons;

namespace BattleService.GameLogic
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddGameLogic(this IServiceCollection s)
        {
            // регистрируем все оружия
            s.AddSingleton<IWeapon, LaserWeapon>();
            s.AddSingleton<IWeapon, RocketLauncher>();
            s.AddSingleton<IWeaponFactory, WeaponFactory>();
            return s;
        }

        private class WeaponFactory(IEnumerable<IWeapon> weapons) : IWeaponFactory
        {
            private readonly Dictionary<string, IWeapon> _map = weapons.ToDictionary(w => w.Code);
            public IWeapon Get(string code) => _map.TryGetValue(code, out var w)
                ? w : throw new KeyNotFoundException(code);
        }
    }
}
