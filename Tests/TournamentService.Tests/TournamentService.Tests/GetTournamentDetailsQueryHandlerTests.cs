using System;
using System.Threading;
using System.Threading.Tasks;
using TournamentService.Application.Queries;
using TournamentService.Domains;
using Xunit;

namespace TournamentService.Tests;

public class GetTournamentDetailsQueryHandlerTests
{
    [Fact]
    public async Task ReturnsNull_When_NotFound()
    {
        var repo = new InMemoryTournamentRepository();
        var handler = new GetTournamentDetailsQueryHandler(repo);

        var result = await handler.Handle(new GetTournamentDetailsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Returns_Tournament_When_Found()
    {
        var repo = new InMemoryTournamentRepository();
        var t = new Tournament { Name = "Alpha", MaxPlayers = 16, Status = TournamentStatus.Upcoming };
        repo.Items.Add(t);

        var handler = new GetTournamentDetailsQueryHandler(repo);
        var result = await handler.Handle(new GetTournamentDetailsQuery(t.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(t.Id, result!.Id);
        Assert.Equal("Alpha", result.Name);
        Assert.Equal(16, result.MaxPlayers);
        Assert.Equal(TournamentStatus.Upcoming, result.Status);
    }
}