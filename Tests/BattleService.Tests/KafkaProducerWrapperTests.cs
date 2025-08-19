using BattleService.Infrastructure.Kafka;
using Confluent.Kafka;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BattleService.Tests
{
    public class KafkaProducerWrapperTests
    {
        [Fact]
        public async Task PublishAsync_Calls_ProduceAsync_WithTopicAndPayload()
        {
            var producer = new Mock<IProducer<string, string>>(MockBehavior.Strict);
            var wrapper = new KafkaProducerWrapper(producer.Object);
            var topic = "battle.created";
            var payload = "{\"hello\":\"world\"}";
            var ct = new CancellationTokenSource().Token;

            producer
                .Setup(p => p.ProduceAsync(topic,
                    It.Is<Message<string, string>>(m => m.Value == payload && !string.IsNullOrWhiteSpace(m.Key)),
                    ct))
                .ReturnsAsync(new DeliveryResult<string, string>());

            await wrapper.PublishAsync(topic, payload, ct);

            producer.VerifyAll();
        }
    }
}
