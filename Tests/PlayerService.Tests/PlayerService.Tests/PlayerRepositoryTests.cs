using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Microsoft.EntityFrameworkCore;
using PlayerService.Domains;
using PlayerService.Infrastructure.Persistence;
using PlayerService.Infrastructure.Repositories;

namespace PlayerService.Tests;

public class PlayerRepositoryTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task Add_Save_Find_Works()
    {
        using var db = CreateDb();
        var repo = new PlayerRepository(db);

        var player = new Player("charlie");
        await repo.AddAsync(player, CancellationToken.None);
        await repo.SaveChangesAsync(CancellationToken.None);

        var loaded = await repo.FindAsync(player.Id, CancellationToken.None);
        Assert.NotNull(loaded);
        Assert.Equal("charlie", loaded!.UserName);
        Assert.Equal(player.Id, loaded.Id);
    }

    [Fact]
    public async Task FindAsync_ReturnsNull_WhenNotFound()
    {
        using var db = CreateDb();
        var repo = new PlayerRepository(db);

        var loaded = await repo.FindAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Null(loaded);
    }
}