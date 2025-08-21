using System;
using Xunit;
using BattleService.Domains;

namespace BattleService.Tests;

public class BattleTests
{
    [Fact]
    public void Initial_State_Is_Waiting_And_Tick_Zero()
    {
        var b = new Battle();
        Assert.Equal(BattleStatus.Waiting, b.Status);
        Assert.Equal(0, b.CurrentTick);
    }

    [Fact]
    public void Start_Transitions_To_Running()
    {
        var b = new Battle();
        b.Start();
        Assert.Equal(BattleStatus.Running, b.Status);
    }

    [Fact]
    public void SubmitTurn_Throws_When_Not_Running()
    {
        var b = new Battle();
        Assert.Throws<InvalidOperationException>(() =>
            b.SubmitTurn(new Turn("p1", 1, Array.Empty<byte>())));
    }

    [Fact]
    public void SubmitTurn_In_Running_Updates_CurrentTick()
    {
        var b = new Battle();
        b.Start();

        b.SubmitTurn(new Turn("p1", 5, Array.Empty<byte>()));
        Assert.Equal(5, b.CurrentTick);

        b.SubmitTurn(new Turn("p1", 3, Array.Empty<byte>()));
        Assert.Equal(5, b.CurrentTick);

        b.SubmitTurn(new Turn("p1", 7, Array.Empty<byte>()));
        Assert.Equal(7, b.CurrentTick);
    }

    [Fact]
    public void Finish_From_Waiting_Transitions_To_Finished()
    {
        var b = new Battle();
        b.Finish();
        Assert.Equal(BattleStatus.Finished, b.Status);
    }

    [Fact]
    public void Finish_From_Running_Transitions_To_Finished()
    {
        var b = new Battle();
        b.Start();
        b.Finish();
        Assert.Equal(BattleStatus.Finished, b.Status);
    }

    [Fact]
    public void SubmitTurn_Throws_After_Finish()
    {
        var b = new Battle();
        b.Start();
        b.Finish();

        Assert.Throws<InvalidOperationException>(() =>
            b.SubmitTurn(new Turn("p1", 10, Array.Empty<byte>())));
    }

    [Fact]
    public void Start_In_Running_Is_NoOp()
    {
        var b = new Battle();
        b.Start();
        var ex = Record.Exception(() => b.Start());
        Assert.Null(ex);
        Assert.Equal(BattleStatus.Running, b.Status);
    }

    [Fact]
    public void Start_From_Finished_Throws()
    {
        var b = new Battle();
        b.Start();
        b.Finish();

        Assert.Throws<InvalidOperationException>(() => b.Start());
    }
}