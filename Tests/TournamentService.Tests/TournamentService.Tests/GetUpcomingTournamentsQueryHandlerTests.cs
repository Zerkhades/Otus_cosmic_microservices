using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentService.Application.Queries;
using TournamentService.Domains;
using Xunit;

namespace TournamentService.Tests;

public class GetUpcomingTournamentsQueryHandlerTests
{
    [Fact]
    public async Task Returns_Only_Upcoming_With_Correct_SlotsLeft()
    {
        var repo = new InMemoryTournamentRepository();
        var up1 = new Tournament { Name = "A", MaxPlayers = 8, Status = TournamentStatus.Upcoming };
        up1.Participants.AddRange(new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }); // 3/8
        var up2 = new Tournament { Name = "B", MaxPlayers = 4, Status = TournamentStatus.Upcoming };
        // 0/4
        var finished = new Tournament { Name = "C", MaxPlayers = 16, Status = TournamentStatus.Finished };

        repo.Items.AddRange(new[] { up1, up2, finished });

        var handler = new GetUpcomingTournamentsQueryHandler(repo);
        var dtos = (await handler.Handle(new GetUpcomingTournamentsQuery(), CancellationToken.None)).ToList();

        Assert.Equal(2, dtos.Count);
        var dto1 = dtos.Single(d => d.Id == up1.Id);
        var dto2 = dtos.Single(d => d.Id == up2.Id);

        Assert.Equal("A", dto1.Name);
        Assert.Equal(8 - 3, dto1.SlotsLeft);

        Assert.Equal("B", dto2.Name);
        Assert.Equal(4 - 0, dto2.SlotsLeft);
    }
}