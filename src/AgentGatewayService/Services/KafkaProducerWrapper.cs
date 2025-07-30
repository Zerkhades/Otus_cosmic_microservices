using Confluent.Kafka;

namespace AgentGatewayService.Services;

public interface IKafkaProducerWrapper
{
    Task PublishAsync(string topic, string payload, CancellationToken ct);
}

public class KafkaProducerWrapper : IKafkaProducerWrapper
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerWrapper> _logger;

    public KafkaProducerWrapper(IProducer<string, string> producer, ILogger<KafkaProducerWrapper> logger)
    {
        _producer = producer;
        _logger = logger;
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken ct)
    {
        try
        {
            var message = new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = payload
            };

            var deliveryResult = await _producer.ProduceAsync(topic, message, ct);
            _logger.LogDebug("Message delivered to {Topic} at partition {Partition}, offset {Offset}",
                deliveryResult.Topic, deliveryResult.Partition, deliveryResult.Offset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing message to Kafka topic {Topic}", topic);
            throw;
        }
    }
}