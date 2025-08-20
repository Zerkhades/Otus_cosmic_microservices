using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TournamentService.Domains;
using Xunit;

namespace TournamentService.Tests;

public class InMemoryTournamentRepositoryTests
{
    [Fact]
    public async Task InsertAsync_Adds_To_Items()
    {
        var repo = new InMemoryTournamentRepository();
        var t = new Tournament { Name = "X", MaxPlayers = 4, Status = TournamentStatus.Upcoming };

        await repo.InsertAsync(t, CancellationToken.None);

        Assert.Single(repo.Items);
        Assert.Equal(t.Id, repo.Items[0].Id);
    }

    [Fact]
    public async Task ReplaceAsync_Replaces_And_Tracks_Id()
    {
        var repo = new InMemoryTournamentRepository();
        var t = new Tournament { Name = "Old", MaxPlayers = 4, Status = TournamentStatus.Upcoming };
        repo.Items.Add(t);

        var updated = new Tournament
        {
            // сохраняем тот же Id для замены
            Id = t.Id,
            Name = "New",
            MaxPlayers = 8,
            Status = TournamentStatus.Upcoming
        };

        await repo.ReplaceAsync(updated, CancellationToken.None);

        Assert.Equal(t.Id, repo.LastReplacedId);
        var stored = repo.Items.Single();
        Assert.Equal("New", stored.Name);
        Assert.Equal(8, stored.MaxPlayers);
    }

    [Fact]
    public async Task ReplaceAsync_Sets_LastReplacedId_When_Item_Missing()
    {
        var repo = new InMemoryTournamentRepository();
        var notExisting = new Tournament { Name = "Nope", MaxPlayers = 2, Status = TournamentStatus.Upcoming };

        await repo.ReplaceAsync(notExisting, CancellationToken.None);

        Assert.Equal(notExisting.Id, repo.LastReplacedId);
        Assert.Empty(repo.Items); // не добавляет если не найден
    }

    [Fact]
    public async Task FindAsync_Returns_Item_Or_Null()
    {
        var repo = new InMemoryTournamentRepository();
        var t1 = new Tournament { Name = "A", MaxPlayers = 4, Status = TournamentStatus.Upcoming };
        var t2 = new Tournament { Name = "B", MaxPlayers = 8, Status = TournamentStatus.Finished };
        repo.Items.AddRange(new[] { t1, t2 });

        var found = await repo.FindAsync(t2.Id, CancellationToken.None);
        var missing = await repo.FindAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(t2.Id, found!.Id);
        Assert.Null(missing);
    }

    [Fact]
    public async Task FilterUpcomingAsync_Returns_Only_Upcoming()
    {
        var repo = new InMemoryTournamentRepository();
        repo.Items.AddRange(new[]
        {
            new Tournament { Name = "U1", MaxPlayers = 4, Status = TournamentStatus.Upcoming },
            new Tournament { Name = "F1", MaxPlayers = 4, Status = TournamentStatus.Finished },
            new Tournament { Name = "U2", MaxPlayers = 4, Status = TournamentStatus.Upcoming }
        });

        var upcoming = await repo.FilterUpcomingAsync(CancellationToken.None);

        Assert.Equal(2, upcoming.Count);
        Assert.All(upcoming, t => Assert.Equal(TournamentStatus.Upcoming, t.Status));
    }
}