using Confluent.Kafka;
using MediatR;
using Newtonsoft.Json.Linq;
using NotificationService.Application.Commands;

namespace NotificationService.Infrastructure.Kafka;

public class KafkaConsumerHostedService : BackgroundService
{
    private readonly IMediator _mediator;
    private readonly ILogger<KafkaConsumerHostedService> _logger;
    private readonly IConsumer<string, string> _consumer;

    public KafkaConsumerHostedService(
        IMediator mediator,
        ILogger<KafkaConsumerHostedService> logger, 
        IConfiguration cfg)
    {
        _mediator = mediator;
        _logger = logger;
        
        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = cfg["Kafka:BootstrapServers"],
            GroupId = "notification-service",
            AutoOffsetReset = AutoOffsetReset.Earliest
        };
        
        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        
        // Subscribe to relevant topics
        _consumer.Subscribe(new[] { 
            "notification.player", 
            "battle.finished", 
            "tournament.registration.accepted",
            "tournament.registration.rejected"
        });
        
        _logger.LogInformation("Kafka consumer initialized and subscribed to topics");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Kafka consumer service started");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(stoppingToken);
                _logger.LogInformation("Received message from topic {Topic}: {Message}", 
                    consumeResult.Topic, consumeResult.Message.Value);
                
                try
                {
                    // Parse the message
                    var message = JObject.Parse(consumeResult.Message.Value);
                    
                    // Extract recipient ID based on message type
                    Guid? recipientId = null;
                    
                    // Different topics might have different structures
                    switch (consumeResult.Topic)
                    {
                        case "battle.finished":
                            // For battle.finished, notify all participants
                            if (message["participants"] is JArray participants)
                            {
                                foreach (var participant in participants)
                                {
                                    if (Guid.TryParse(participant.ToString(), out var playerId))
                                    {
                                        await CreateNotification(playerId, consumeResult.Topic, message);
                                    }
                                }
                            }
                            break;
                            
                        case "tournament.registration.accepted":
                        case "tournament.registration.rejected":
                            // For tournament events, recipient is the playerId
                            if (message["playerId"] != null && 
                                Guid.TryParse(message["playerId"].ToString(), out var tournamentPlayerId))
                            {
                                recipientId = tournamentPlayerId;
                            }
                            break;
                            
                        case "notification.player":
                            // Direct notification to a player
                            if (message["recipientId"] != null && 
                                Guid.TryParse(message["recipientId"].ToString(), out var directRecipientId))
                            {
                                recipientId = directRecipientId;
                            }
                            break;
                    }
                    
                    // Create notification for single recipient if applicable
                    if (recipientId.HasValue)
                    {
                        await CreateNotification(recipientId.Value, consumeResult.Topic, message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Kafka message from topic {Topic}", consumeResult.Topic);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Kafka consumer");
                
                // Brief pause to prevent tight loop in case of persistent errors
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task CreateNotification(Guid recipientId, string topic, JObject payload)
    {
        try
        {
            await _mediator.Send(new EnqueueNotificationCommand(
                recipientId,
                topic,
                payload.ToString()
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification for recipient {RecipientId}", recipientId);
        }
    }

    public override void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        base.Dispose();
    }
}