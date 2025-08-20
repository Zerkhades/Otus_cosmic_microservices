using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TournamentService.Infrastructure.Persistence;
using Xunit;

namespace TournamentService.Tests;

public class MongoContextTests
{
    [Fact]
    public void Tournaments_Collection_Has_Correct_Names_And_Is_Not_Null()
    {
        var opts = Options.Create(new MongoSettings
        {
            ConnectionString = "mongodb://localhost:27017",
            Database = "UnitTestDb"
        });

        var ctx = new MongoContext(opts);
        var collection = ctx.Tournaments;

        Assert.NotNull(collection);
        Assert.Equal("UnitTestDb", collection.Database.DatabaseNamespace.DatabaseName);
        Assert.Equal("tournaments", collection.CollectionNamespace.CollectionName);
    }
}