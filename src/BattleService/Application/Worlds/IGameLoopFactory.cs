namespace BattleService.Application.Worlds
{
    public interface IGameLoopFactory
    {
        GameLogic.Engine.GameLoop Create(Guid battleId);
    }
}
