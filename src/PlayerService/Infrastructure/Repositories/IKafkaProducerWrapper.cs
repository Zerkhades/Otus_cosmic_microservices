using Confluent.Kafka;

namespace PlayerService.Infrastructure.Repositories;

public interface IKafkaProducerWrapper
{
    Task PublishAsync(string topic, string payload, CancellationToken ct);
}