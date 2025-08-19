using System;
using Xunit;
using PlayerService.Domains;

namespace PlayerService.Tests;

public class PlayerDomainTests
{
    [Fact]
    public void Ctor_Sets_UserName_DefaultRating_And_RegisteredAt()
    {
        var p = new Player("alice");

        Assert.Equal("alice", p.UserName);
        Assert.True(p.Rating >= 1000);
        Assert.True((DateTime.UtcNow - p.RegisteredAt).TotalSeconds < 5);
        Assert.NotEqual(Guid.Empty, p.Id);
    }

    [Fact]
    public void UpdateRating_Changes_Rating()
    {
        var p = new Player("bob");
        var start = p.Rating;

        p.UpdateRating(+25);
        Assert.Equal(start + 25, p.Rating);

        p.UpdateRating(-10);
        Assert.Equal(start + 15, p.Rating);
    }
}