using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Engine;

namespace BattleService.GameLogic.Commands
{
    public record TurnCommand(Guid ShipId, float DegDelta) : ICommand
    {
        public void Execute(GameContext ctx)
        {
            if (ctx.Ships.TryGetValue(ShipId, out var ship))
                ship.Rotate(DegDelta);
        }
    }

}
