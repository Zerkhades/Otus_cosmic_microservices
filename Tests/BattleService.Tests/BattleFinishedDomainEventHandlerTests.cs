using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BattleService.Application.Events;
using Microsoft.Extensions.Logging;
using Xunit;

namespace BattleService.Tests
{
    // Фейковый логгер, который собирает логи в коллекцию для проверки
    public class FakeLogger<T> : ILogger<T>
    {
        public List<LogEntry> Logs { get; } = new();

        public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add(new LogEntry
            {
                LogLevel = logLevel,
                Message = formatter(state, exception),
                Exception = exception
            });
        }

        public class LogEntry
        {
            public LogLevel LogLevel { get; set; }
            public string Message { get; set; } = string.Empty;
            public Exception? Exception { get; set; }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();
            public void Dispose() { }
        }
    }

    public class BattleFinishedDomainEventHandlerTests
    {
        [Fact]
        public async Task Handle_ShouldLogInformationMessage()
        {
            // Arrange
            var fakeLogger = new FakeLogger<BattleFinishedDomainEventHandler>();
            var handler = new BattleFinishedDomainEventHandler(fakeLogger);

            var battleId = Guid.NewGuid();
            var tournamentId = Guid.NewGuid();
            var participants = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

            var notification = new BattleFinishedDomainEvent(battleId, tournamentId, participants);

            // Act
            await handler.Handle(notification, CancellationToken.None);

            // Assert
            Assert.Contains(fakeLogger.Logs, log =>
                log.LogLevel == LogLevel.Information &&
                log.Message.Contains(battleId.ToString()) &&
                log.Message.Contains(tournamentId.ToString()) &&
                log.Message.Contains(participants.Count.ToString()));
        }
    }
}