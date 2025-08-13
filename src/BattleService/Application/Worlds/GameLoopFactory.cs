using BattleService.GameLogic.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace BattleService.Application.Worlds;

public sealed class GameLoopFactory : IGameLoopFactory
{
    private readonly IServiceProvider _sp;
    public GameLoopFactory(IServiceProvider sp) => _sp = sp;

    public GameLoop Create(Guid battleId)
    {
        // Если у GameLoop есть зависимости — ActivatorUtilities их подтянет.
        // При желании сюда можно передать battleId, если конструктор поддерживает.
        return ActivatorUtilities.CreateInstance<GameLoop>(_sp);
    }
}