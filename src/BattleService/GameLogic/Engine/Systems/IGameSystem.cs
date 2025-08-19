using System;

namespace BattleService.GameLogic.Engine.Systems
{
    /// <summary>Мини-система симуляции одного шага.</summary>
    public interface IGameSystem
    {
        void Update(GameContext ctx, float dt);
    }
}