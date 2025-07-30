using BattleService.GameLogic.Engine;

namespace BattleService.GameLogic.Abstractions
{
    public interface ICommand
    {
        void Execute(GameContext ctx);
    }
}
