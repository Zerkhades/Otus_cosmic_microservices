namespace BattleService.GameLogic.Engine.Systems
{
    internal sealed class SpatialIndexSystem : IGameSystem
    {
        private readonly SpatialHashGrid _grid;

        public SpatialIndexSystem(SpatialHashGrid grid) => _grid = grid;

        public void Update(GameContext ctx, float dt)
        {
            _grid.Clear();
            foreach (var s in ctx.Ships.Values) _grid.Insert(s);
            foreach (var p in ctx.Projectiles.Values)
                if (p.IsAlive) _grid.Insert(p);
        }
    }
}