using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentService.Domains;
using TournamentService.Infrastructure.Repositories;
using TournamentService.Infrastructure.Repositories.Kafka;
using Microsoft.Extensions.Logging;

namespace TournamentService.Tests;

internal sealed class InMemoryTournamentRepository : ITournamentRepository
{
    public readonly List<Tournament> Items = new();
    public Guid? LastReplacedId { get; private set; }

    public Task InsertAsync(Tournament entity, CancellationToken ct)
    {
        Items.Add(entity);
        return Task.CompletedTask;
    }

    public Task ReplaceAsync(Tournament entity, CancellationToken ct)
    {
        var idx = Items.FindIndex(t => t.Id == entity.Id);
        if (idx >= 0) Items[idx] = entity;
        LastReplacedId = entity.Id;
        return Task.CompletedTask;
    }

    public Task<Tournament?> FindAsync(Guid id, CancellationToken ct) =>
        Task.FromResult<Tournament?>(Items.FirstOrDefault(t => t.Id == id));

    public Task<List<Tournament>> FilterUpcomingAsync(CancellationToken ct) =>
        Task.FromResult(Items.Where(t => t.Status == TournamentStatus.Upcoming).ToList());
}

internal sealed class FakeKafkaProducerWrapper : IKafkaProducerWrapper
{
    public readonly List<(string Topic, string Payload)> Published = new();

    public Task PublishAsync(string topic, string payload, CancellationToken ct)
    {
        Published.Add((topic, payload));
        return Task.CompletedTask;
    }
}

internal sealed class NoopLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }

    private sealed class NullScope : IDisposable { public static readonly NullScope Instance = new(); public void Dispose() { } }
}