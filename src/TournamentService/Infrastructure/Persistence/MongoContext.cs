using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TournamentService.Domains;

namespace TournamentService.Infrastructure.Persistence;

public class MongoSettings
{
    public string ConnectionString { get; set; } = string.Empty;
    public string Database { get; set; } = "CosmicBattles";
}

public class MongoContext
{
    private readonly IMongoDatabase _db;
    public MongoContext(IOptions<MongoSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _db = client.GetDatabase(settings.Value.Database);
    }

    public IMongoCollection<Tournament> Tournaments => _db.GetCollection<Tournament>("tournaments");
}