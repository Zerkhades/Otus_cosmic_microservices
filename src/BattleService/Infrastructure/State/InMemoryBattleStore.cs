using System.Collections.Concurrent;
using BattleService.Domains;

namespace BattleService.Infrastructure.State;

public interface IBattleStore
{
    void Add(Battle battle);
    bool TryGet(Guid id, out Battle? battle);
    void Remove(Guid id);
    IEnumerable<Battle> ListRunning();
}

public class InMemoryBattleStore : IBattleStore
{
    private readonly ConcurrentDictionary<Guid, Battle> _dict = new();

    public void Add(Battle battle) => _dict.TryAdd(battle.Id, battle);
    public bool TryGet(Guid id, out Battle? battle) => _dict.TryGetValue(id, out battle);
    public void Remove(Guid id) => _dict.TryRemove(id, out _);
    public IEnumerable<Battle> ListRunning() => _dict.Values.Where(b => b.Status == BattleStatus.Running);
}