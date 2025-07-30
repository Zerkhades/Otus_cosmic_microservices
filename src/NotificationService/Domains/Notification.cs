namespace NotificationService.Domains;

public enum ChannelType { WebSocket, Email, Push }

public class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RecipientId { get; init; }
    public string Type { get; init; } = string.Empty; // e.g. battle.finished
    public string PayloadJson { get; init; } = "{}";
    public bool IsSent { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}