using BattleService.GameLogic.Entities;

namespace BattleService.GameLogic.Engine
{
    internal sealed class SpatialHashGrid
    {
        private readonly float _cell;
        private readonly Dictionary<long, Cell> _cells = new();

        private sealed class Cell
        {
            public readonly List<Ship> Ships = new();
            public readonly List<Projectile> Projectiles = new();
        }

        public SpatialHashGrid(float cellSize) => _cell = MathF.Max(8f, cellSize);

        private static long Pack(int x, int y) => ((long)x << 32) ^ (uint)y;

        private (int cx, int cy) CellOf(Vector2 p)
            => ((int)MathF.Floor(p.X / _cell), (int)MathF.Floor(p.Y / _cell));

        public void Clear() => _cells.Clear();

        public void Insert(Ship s)
        {
            var (cx, cy) = CellOf(s.Position);
            var key = Pack(cx, cy);
            if (!_cells.TryGetValue(key, out var cell))
                _cells[key] = cell = new Cell();
            cell.Ships.Add(s);
        }

        public void Insert(Projectile p)
        {
            var (cx, cy) = CellOf(p.Position);
            var key = Pack(cx, cy);
            if (!_cells.TryGetValue(key, out var cell))
                _cells[key] = cell = new Cell();
            cell.Projectiles.Add(p);
        }

        public IEnumerable<Ship> QueryShipsAround(Vector2 pos)
        {
            var (cx, cy) = CellOf(pos);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var key = Pack(cx + dx, cy + dy);
                    if (_cells.TryGetValue(key, out var cell))
                        foreach (var s in cell.Ships) yield return s;
                }
        }

        public IEnumerable<Projectile> QueryProjectilesAround(Vector2 pos)
        {
            var (cx, cy) = CellOf(pos);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    var key = Pack(cx + dx, cy + dy);
                    if (_cells.TryGetValue(key, out var cell))
                        foreach (var p in cell.Projectiles) yield return p;
                }
        }
    }
}