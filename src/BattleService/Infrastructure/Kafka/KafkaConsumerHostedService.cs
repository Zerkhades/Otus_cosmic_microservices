// Updated KafkaConsumerHostedService.cs (only constructor + creation spot changed)
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
    private readonly IKafkaConsumerFactory _consumerFactory;

    public KafkaConsumerHostedService(
        ILogger<KafkaConsumerHostedService> logger,
        IConfiguration cfg,
        IMediator mediator,
        IHostApplicationLifetime appLifetime,
        IKafkaConsumerFactory consumerFactory)   // <-- injected
    {
        _logger = logger;
        _cfg = cfg;
        _mediator = mediator;
        _appLifetime = appLifetime;
        _consumerFactory = consumerFactory;
    }

    private IConsumer<string, string>? _consumer;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var tcsStarted = new TaskCompletionSource();
        using var reg = _appLifetime.ApplicationStarted.Register(() => tcsStarted.TrySetResult());
        await tcsStarted.Task.WaitAsync(stoppingToken);

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = _cfg["Kafka:BootstrapServers"],
            GroupId = "battle-service",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            SocketTimeoutMs = 10000,
            EnableAutoCommit = true
        };

        _logger.LogInformation("Starting Kafka consumer for topic 'battle.created'...");

        using var consumer = _consumerFactory.Create(consumerConfig, _logger); // <-- here
        _consumer = consumer;

        try
        {
            consumer.Subscribe("battle.created");
            _logger.LogInformation("Kafka consumer subscribed and running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var cr = consumer.Consume(TimeSpan.FromSeconds(1));
                    if (cr == null) continue;
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
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // graceful shutdown during backoff
                    }
                }
                catch (OperationCanceledException)
                {
                    // graceful shutdown
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in consumer loop");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // graceful shutdown during backoff
                    }
                }
            }
        }
        finally
        {
            try
            {
                consumer.Close();
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
