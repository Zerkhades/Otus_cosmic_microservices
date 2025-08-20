using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PlayerService.Application.Queries;
using PlayerService.Domains;
using PlayerService.Infrastructure.Persistence;
using Xunit;

namespace PlayerService.Tests;

public class GetPlayerQueryHandlerTests
{
    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task Returns_Dto_When_Player_Exists()
    {
        using var db = CreateDb();
        var p = new Player("eva", 1337);
        db.Players.Add(p);
        await db.SaveChangesAsync();

        var handler = new GetPlayerQueryHandler(db);
        var dto = await handler.Handle(new GetPlayerQuery(p.Id), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(p.Id, dto!.Id);
        Assert.Equal("eva", dto.UserName);
        Assert.Equal(1337, dto.Rating);
    }

    [Fact]
    public async Task Returns_Null_When_NotFound()
    {
        using var db = CreateDb();
        var handler = new GetPlayerQueryHandler(db);

        var dto = await handler.Handle(new GetPlayerQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(dto);
    }
}