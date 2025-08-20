namespace PlayerService.Domains;

public class Player
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string UserName { get; private set; } = string.Empty;
    public int Rating { get; private set; }
    public DateTime RegisteredAt { get; private set; } = DateTime.UtcNow;

    private Player() { }
    
    public Player(string userName, int rating = 1000)
    {
        UserName = userName;
        Rating = rating;
    }

    public void UpdateRating(int delta) => Rating += delta;
}