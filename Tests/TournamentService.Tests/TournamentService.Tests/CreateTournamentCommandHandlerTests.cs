using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TournamentService.Application.Commands;
using TournamentService.Domains;
using Xunit;

namespace TournamentService.Tests;

public class CreateTournamentCommandHandlerTests
{
    [Fact]
    public async Task Handle_Inserts_Upcoming_Tournament_And_Publishes_Created_Event()
    {
        var repo = new InMemoryTournamentRepository();
        var producer = new FakeKafkaProducerWrapper();
        var logger = new NoopLogger<CreateTournamentCommandHandler>();

        var handler = new CreateTournamentCommandHandler(repo, producer, logger);

        var starts = DateTime.UtcNow.AddDays(2);
        var id = await handler.Handle(new CreateTournamentCommand("Spring Cup", starts, 32, "{}"), CancellationToken.None);

        // Репозиторий получил сущность
        Assert.Single(repo.Items);
        var entity = repo.Items[0];
        Assert.Equal(id, entity.Id);
        Assert.Equal("Spring Cup", entity.Name);
        Assert.Equal(starts, entity.StartsAt);
        Assert.Equal(32, entity.MaxPlayers);
        Assert.Equal(TournamentStatus.Upcoming, entity.Status);

        // Отправлено событие tournament.created
        Assert.Single(producer.Published);
        var evt = producer.Published[0];
        Assert.Equal("tournament.created", evt.Topic);

        using var doc = JsonDocument.Parse(evt.Payload);
        var root = doc.RootElement;
        Assert.Equal(entity.Id.ToString(), root.GetProperty("tournamentId").GetString());
        Assert.Equal("Spring Cup", root.GetProperty("name").GetString());
        Assert.Equal("Upcoming", root.GetProperty("status").GetString());
    }
}