namespace BattleService.Domains;

public enum BattleStatus { Waiting, Running, Finished }

public class Battle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TournamentId { get; init; }
    public List<Guid> Participants { get; init; } = new();
    public int CurrentTick { get; private set; }
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public BattleStatus Status { get; private set; } = BattleStatus.Waiting;
    private readonly List<Turn> _turns = new();

    public void Start() => Status = BattleStatus.Running;

    public void SubmitTurn(Turn turn)
    {
        if (Status != BattleStatus.Running) throw new InvalidOperationException("Battle not running");
        _turns.Add(turn);
        CurrentTick = Math.Max(CurrentTick, turn.Tick);
    }

    public void Finish() => Status = BattleStatus.Finished;
}