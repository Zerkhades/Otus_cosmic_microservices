using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BattleService.Application.Worlds;

public sealed class WorldCleanupService : BackgroundService
{
    private readonly BattleWorldManager _mgr;
    private readonly ILogger<WorldCleanupService> _log;
    private readonly TimeSpan _check = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _idle = TimeSpan.FromMinutes(2);

    public WorldCleanupService(BattleWorldManager mgr, ILogger<WorldCleanupService> log)
    {
        _mgr = mgr; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var w in _mgr.Worlds)
                {
                    if (w.ClientsCount == 0 && DateTime.UtcNow - w.LastActivityUtc > _idle)
                        _mgr.Remove(w.BattleId);
                }
            }
            catch (Exception ex) { _log.LogError(ex, "Cleanup tick failed"); }

            await Task.Delay(_check, stoppingToken);
        }
    }
}
