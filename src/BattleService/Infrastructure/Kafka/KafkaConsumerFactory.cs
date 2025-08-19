using Confluent.Kafka;

namespace BattleService.Infrastructure.Kafka
{
    public class KafkaConsumerFactory : IKafkaConsumerFactory
    {
        public IConsumer<string, string> Create(ConsumerConfig config, ILogger logger)
            => new ConsumerBuilder<string, string>(config)
                .SetErrorHandler((_, e) => logger.LogWarning("Kafka error: {Reason}", e.Reason))
                .Build();
    }
}
