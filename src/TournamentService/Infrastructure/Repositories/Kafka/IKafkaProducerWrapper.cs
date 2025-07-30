using Confluent.Kafka;

namespace TournamentService.Infrastructure.Repositories.Kafka;

public interface IKafkaProducerWrapper
{
    Task PublishAsync(string topic, string payload, CancellationToken ct);
}

public class KafkaProducerWrapper : IKafkaProducerWrapper
{
    private readonly IProducer<string, string> _producer;
    public KafkaProducerWrapper(IProducer<string, string> producer) => _producer = producer;

    public Task PublishAsync(string topic, string payload, CancellationToken ct)
        => _producer.ProduceAsync(topic, new Message<string, string> { Key = Guid.NewGuid().ToString(), Value = payload }, ct);
}