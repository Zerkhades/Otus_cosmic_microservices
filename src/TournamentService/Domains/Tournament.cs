using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TournamentService.Domains;

public enum TournamentStatus { Draft, Upcoming, Running, Finished }

public class Tournament
{
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime StartsAt { get; set; }
    public int MaxPlayers { get; set; }
    public string RuleSetJson { get; set; } = "{}";
    public TournamentStatus Status { get; set; } = TournamentStatus.Draft;
    [BsonRepresentation(BsonType.String)]
    public List<Guid> Participants { get; set; } = new();
    public int Rating { get; set; }
}