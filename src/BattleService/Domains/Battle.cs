namespace BattleService.Domains;

// STATE Pattern
public enum BattleStatus { Waiting, Running, Finished }

public interface IBattleState
{
    BattleStatus Status { get; }
    void Start(Battle ctx);
    void SubmitTurn(Battle ctx, Turn turn);
    void Finish(Battle ctx);
}

public sealed class WaitingState : IBattleState
{
    public BattleStatus Status => BattleStatus.Waiting;

    public void Start(Battle ctx) => ctx.TransitionTo(new RunningState());

    public void SubmitTurn(Battle ctx, Turn turn) =>
        throw new InvalidOperationException("Battle not running");

    public void Finish(Battle ctx) => ctx.TransitionTo(new FinishedState());
}

public sealed class RunningState : IBattleState
{
    public BattleStatus Status => BattleStatus.Running;

    public void Start(Battle ctx) { /* no-op */ }

    public void SubmitTurn(Battle ctx, Turn turn) => ctx.InternalAddTurn(turn);

    public void Finish(Battle ctx) => ctx.TransitionTo(new FinishedState());
}

public sealed class FinishedState : IBattleState
{
    public BattleStatus Status => BattleStatus.Finished;

    public void Start(Battle ctx) =>
        throw new InvalidOperationException("Battle finished");

    public void SubmitTurn(Battle ctx, Turn turn) =>
        throw new InvalidOperationException("Battle finished");

    public void Finish(Battle ctx) { /* no-op */ }
}

public class Battle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TournamentId { get; init; }
    public List<Guid> Participants { get; init; } = new();
    public int CurrentTick { get; private set; }
    public DateTime StartTime { get; init; } = DateTime.UtcNow;
    public BattleStatus Status { get; private set; } = BattleStatus.Waiting;

    private readonly List<Turn> _turns = new();
    private IBattleState _state = new WaitingState();

    public void Start() => _state.Start(this);

    public void SubmitTurn(Turn turn) => _state.SubmitTurn(this, turn);

    public void Finish() => _state.Finish(this);

    internal void InternalAddTurn(Turn turn)
    {
        _turns.Add(turn);
        CurrentTick = Math.Max(CurrentTick, turn.Tick);
    }

    internal void TransitionTo(IBattleState next)
    {
        _state = next;
        Status = next.Status;
    }
}