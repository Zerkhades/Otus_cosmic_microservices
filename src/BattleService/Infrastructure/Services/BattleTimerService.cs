using BattleService.Application.Commands;
using BattleService.Domains;
using BattleService.Infrastructure.State;
using MediatR;

namespace BattleService.Infrastructure.Services;

public class BattleTimerService : BackgroundService
{
    private readonly IBattleStore _store;
    private readonly IMediator _mediator;
    private readonly ILogger<BattleTimerService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(10);
    private readonly TimeSpan _maxBattleDuration = TimeSpan.FromMinutes(1); // Configurable via settings

    public BattleTimerService(IBattleStore store, IMediator mediator, ILogger<BattleTimerService> logger)
    {
        _store = store;
        _mediator = mediator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Battle timer service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Check for battles that need to be finished
                var runningBattles = _store.ListRunning().ToList();
                if (runningBattles.Any())
                {
                    _logger.LogInformation("Found {Count} running battles", runningBattles.Count);

                    foreach (var battle in runningBattles)
                    {
                        // Check if the battle has exceeded the maximum duration
                        if (battle.Status == BattleStatus.Running &&
                            battle.StartTime.Add(_maxBattleDuration) <= DateTime.UtcNow)
                        {
                            _logger.LogInformation("Finishing battle {BattleId} due to timeout", battle.Id);
                            await _mediator.Send(new FinishBattleCommand(battle.Id), stoppingToken);
                        }
                    }
                }
                else
                    _logger.LogInformation("No battles found.");

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in battle timer service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }
}