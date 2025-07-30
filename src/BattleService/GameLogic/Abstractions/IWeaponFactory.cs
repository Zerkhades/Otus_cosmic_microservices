namespace BattleService.GameLogic.Abstractions
{
    /// <summary>
    /// Фабрика, через которую игровые команды создают экземпляры оружия по строковому коду.
    /// DI инжектирует в неё все зарегистрированные IWeapon.
    /// </summary>
    public interface IWeaponFactory { IWeapon Get(string code); }
}
