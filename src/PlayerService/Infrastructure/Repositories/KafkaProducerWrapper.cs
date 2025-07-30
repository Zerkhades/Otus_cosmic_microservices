using Confluent.Kafka;

namespace PlayerService.Infrastructure.Repositories;

public class KafkaProducerWrapper : IKafkaProducerWrapper
{
    private readonly IProducer<string, string> _producer;
    
    public KafkaProducerWrapper(IProducer<string, string> producer)
    {
        _producer = producer;
    }

    public async Task PublishAsync(string topic, string payload, CancellationToken ct)
    {
        var message = new Message<string, string>
        {
            Key = Guid.NewGuid().ToString(),
            Value = payload
        };
        
        await _producer.ProduceAsync(topic, message, ct);
    }
}