using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace BattleService.Application.Worlds;

public sealed class BattleWorldManager
{
    private readonly ConcurrentDictionary<Guid, GameWorld> _worlds = new();
    private readonly IGameLoopFactory _factory;
    private readonly ILogger<BattleWorldManager> _log;

    public BattleWorldManager(IGameLoopFactory factory, ILogger<BattleWorldManager> log)
    {
        _factory = factory;
        _log = log;
    }

    public GameWorld GetOrCreate(Guid battleId)
    {
        return _worlds.GetOrAdd(battleId, id =>
        {
            var world = new GameWorld(id, _factory, _log);
            world.Start();
            _log.LogInformation("Created world for battle {BattleId}", id);
            return world;
        });
    }

    public IReadOnlyCollection<GameWorld> Worlds => (IReadOnlyCollection<GameWorld>)_worlds.Values;

    public void Remove(Guid battleId)
    {
        if (_worlds.TryRemove(battleId, out var w))
        {
            w.Stop();
            _log.LogInformation("Removed world for battle {BattleId}", battleId);
        }
    }
}
