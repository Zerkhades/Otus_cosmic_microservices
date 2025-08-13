using System.Text.Json;
using BattleService.Application.Commands;
using Confluent.Kafka;
using MediatR;

namespace BattleService.Infrastructure.Kafka;

public class KafkaConsumerHostedService : BackgroundService
{
    private readonly ILogger<KafkaConsumerHostedService> _logger;
    private readonly IConfiguration _cfg;
    private readonly IMediator _mediator;
    private readonly IHostApplicationLifetime _appLifetime;

    public KafkaConsumerHostedService(
        ILogger<KafkaConsumerHostedService> logger,
        IConfiguration cfg,
        IMediator mediator,
        IHostApplicationLifetime appLifetime)
    {
        _logger = logger;
        _cfg = cfg;
        _mediator = mediator;
        _appLifetime = appLifetime;
    }

    private IConsumer<string, string>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Дождаться полного старта веб-приложения (Kestrel и т.п.)
        var tcsStarted = new TaskCompletionSource();
        using var reg = _appLifetime.ApplicationStarted.Register(() => tcsStarted.TrySetResult());
        await tcsStarted.Task.WaitAsync(stoppingToken);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _cfg["Kafka:BootstrapServers"],
            GroupId = "battle-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Чтобы не виснуть в бесконечных DNS/сокет операциях
            SocketTimeoutMs = 10000,
            // По желанию: снизит шум ретраев при потере коннекта
            EnableAutoCommit = true
        };

        _logger.LogInformation("Starting Kafka consumer for topic 'battle.created'...");

        // Создаём consumer только здесь, а не в конструкторе
        using var consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetErrorHandler((_, e) => _logger.LogWarning("Kafka error: {Reason}", e.Reason))
            .Build();

        _consumer = consumer;

        try
        {
            consumer.Subscribe("battle.created");
            _logger.LogInformation("Kafka consumer subscribed and running.");

            // Основной цикл: короткие таймауты, чтобы реагировать на отмену
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (cr == null) continue; // таймаут — просто проверили токен и дальше

                    if (cr.IsPartitionEOF) continue;

                    _logger.LogInformation("Received battle.created event: {Payload}", cr.Message.Value);

                    try
                    {
                        using var payload = JsonDocument.Parse(cr.Message.Value);
                        var root = payload.RootElement;

                        if (root.TryGetProperty("battleId", out var battleIdElement) &&
                            root.TryGetProperty("tournamentId", out var tournamentIdElement) &&
                            root.TryGetProperty("participants", out var participantsElement))
                        {
                            var battleId = Guid.Parse(battleIdElement.GetString()!);
                            var tournamentId = Guid.Parse(tournamentIdElement.GetString()!);

                            var participants = new List<Guid>();
                            foreach (var participant in participantsElement.EnumerateArray())
                                participants.Add(Guid.Parse(participant.GetString()!));

                            await _mediator.Send(new StartBattleCommand(battleId, tournamentId, participants), stoppingToken);

                            _logger.LogInformation(
                                "Started battle {BattleId} for tournament {TournamentId} with {Count} participants",
                                battleId, tournamentId, participants.Count);
                        }
                        else
                        {
                            _logger.LogWarning("Missing required fields in battle.created payload");
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, "Error parsing battle.created event payload");
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.LogWarning(ex, "Kafka consume exception (will retry)");
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in consumer loop");
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
            }
        }
        finally
        {
            try
            {
                consumer.Close(); // корректный коммит оффсетов и выход из группы
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error while closing Kafka consumer");
            }
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
