using BattleService.GameLogic.Abstractions;
using BattleService.GameLogic.Engine;

namespace BattleService.GameLogic.Commands
{
    public record MoveCommand(Guid ShipId, float ThrustDelta) : ICommand
    {
        public void Execute(GameContext ctx)
        {
            if (ctx.Ships.TryGetValue(ShipId, out var ship))
                ship.ApplyThrust(ThrustDelta);
        }
    }
}
