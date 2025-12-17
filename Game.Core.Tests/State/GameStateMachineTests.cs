using Game.Core.State;
using Xunit;

namespace Game.Core.Tests.State;

public class GameStateMachineTests
{
    [Fact]
    public void Transitions_Follow_Happy_Path_And_Fire_Events()
    {
        var fsm = new GameStateMachine();
        int calls = 0;
        fsm.OnTransition += (prev, next) => calls++;

        Assert.True(fsm.Start());
        Assert.True(fsm.Pause());
        Assert.True(fsm.Resume());
        Assert.True(fsm.End());

        Assert.Equal(GameFlowState.GameOver, fsm.State);
        Assert.True(calls >= 3);
    }

    [Fact]
    public void Invalid_Transitions_Are_Rejected()
    {
        var fsm = new GameStateMachine();
        Assert.False(fsm.Resume());
        Assert.True(fsm.End());
        Assert.False(fsm.End());
        Assert.False(fsm.Start());
    }

    [Fact]
    public void Pause_Returns_False_When_Not_In_Running_State()
    {
        var fsm = new GameStateMachine();

        // Pause in Initialized state should return false
        Assert.Equal(GameFlowState.Initialized, fsm.State);
        Assert.False(fsm.Pause());

        // Transition to GameOver and try to pause
        Assert.True(fsm.End());
        Assert.Equal(GameFlowState.GameOver, fsm.State);
        Assert.False(fsm.Pause());
    }
}
