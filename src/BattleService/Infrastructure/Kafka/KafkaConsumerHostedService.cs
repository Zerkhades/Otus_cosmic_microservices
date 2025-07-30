using System.Text.Json;
using BattleService.Application.Commands;
using Confluent.Kafka;
using MediatR;

namespace BattleService.Infrastructure.Kafka;

public class KafkaConsumerHostedService : BackgroundService
{
    private readonly ILogger<KafkaConsumerHostedService> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly IMediator _mediator;

    public KafkaConsumerHostedService(
        ILogger<KafkaConsumerHostedService> logger, 
        IConfiguration cfg, 
        IMediator mediator)
    {
        _logger = logger;
        _mediator = mediator;
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = cfg["Kafka:BootstrapServers"],
            GroupId = "battle-service",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        _consumer.Subscribe("battle.created");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kafka consumer started, listening for battle.created events");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cr = _consumer.Consume(stoppingToken);
                _logger.LogInformation("Received battle.created event: {Payload}", cr.Message.Value);
                
                try
                {
                    var payload = JsonDocument.Parse(cr.Message.Value);
                    var root = payload.RootElement;
                    
                    if (root.TryGetProperty("battleId", out var battleIdElement) && 
                        root.TryGetProperty("tournamentId", out var tournamentIdElement) && 
                        root.TryGetProperty("participants", out var participantsElement))
                    {
                        var battleId = Guid.Parse(battleIdElement.GetString()!);
                        var tournamentId = Guid.Parse(tournamentIdElement.GetString()!);
                        
                        var participants = new List<Guid>();
                        foreach (var participant in participantsElement.EnumerateArray())
                        {
                            participants.Add(Guid.Parse(participant.GetString()!));
                        }
                        
                        await _mediator.Send(new StartBattleCommand(battleId, tournamentId, participants), stoppingToken);
                        _logger.LogInformation("Started battle {BattleId} for tournament {TournamentId} with {ParticipantCount} participants", 
                            battleId, tournamentId, participants.Count);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Error parsing battle.created event payload");
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming battle.created event");
            }
        }
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        base.Dispose();
    }
}