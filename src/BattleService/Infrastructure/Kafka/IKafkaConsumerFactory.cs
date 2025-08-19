using Confluent.Kafka;

namespace BattleService.Infrastructure.Kafka
{
    public interface IKafkaConsumerFactory
    {
        IConsumer<string, string> Create(ConsumerConfig config, ILogger logger);
    }
}
