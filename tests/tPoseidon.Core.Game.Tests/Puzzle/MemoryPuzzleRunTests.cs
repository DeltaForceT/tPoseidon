using tPoseidon.Core.Game.Puzzle;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Puzzle;

public sealed class MemoryPuzzleRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    private static readonly MemoryPuzzleEntry Puzzle = new(1, 1, "MemoryPuzzleObject_001", 3, 60, 656, []);
    private static readonly (int, int)[] Order = [(2, 3034), (4, 3034), (1, 3034)];

    private static MemoryPuzzleRun Shown()
    {
        var run = new MemoryPuzzleRun(Puzzle, 650, Order, Now);
        for (var second = 0; second < 3; second++)
        {
            Assert.NotNull(run.NextToShow(Now.AddSeconds(second)));
            Assert.Null(run.NextToShow(Now.AddSeconds(second)));
            run.Shown((ushort)(100 + second));
        }
        return run;
    }

    [Fact]
    public void StatuesShowOneASecondThenInputStarts()
    {
        var run = Shown();
        Assert.False(run.StartInputIfShown(Now.AddSeconds(2.5)));
        Assert.True(run.StartInputIfShown(Now.AddSeconds(3)));
        Assert.Equal(Now.AddSeconds(63), run.Deadline);
    }

    [Fact]
    public void KillsInSpawnOrderSolve()
    {
        var run = Shown();
        run.StartInputIfShown(Now.AddSeconds(3));
        run.Killed(100);
        run.Killed(101);
        Assert.Equal(MemoryPuzzlePhase.Input, run.Phase);
        run.Killed(102);
        Assert.Equal(PuzzleRules.Solved, run.Result);
        Assert.Equal(7u, run.RecordSeconds(Now.AddSeconds(10)));
    }

    [Fact]
    public void WrongOrderEarlyKillOrTimeOutFails()
    {
        var wrong = Shown();
        wrong.StartInputIfShown(Now.AddSeconds(3));
        wrong.Killed(101);
        Assert.Equal(PuzzleRules.Failed, wrong.Result);
        var early = Shown();
        early.Killed(100);
        Assert.Equal(PuzzleRules.Failed, early.Result);
        var slow = Shown();
        slow.StartInputIfShown(Now.AddSeconds(3));
        Assert.False(slow.TimedOut(Now.AddSeconds(62)));
        Assert.True(slow.TimedOut(Now.AddSeconds(63)));
        Assert.Equal(0u, slow.RecordSeconds(Now.AddSeconds(63)));
    }

    [Fact]
    public void OrderUsesOnlyPositionsOnTheMap()
    {
        (int, int)[] objects = [(1, 3034), (2, 3034), (9, 3034), (13, 3034)];
        var order = MemoryPuzzleRun.ChooseOrder(objects, new HashSet<int> { 1, 2, 3 }, 5, _ => 0);
        Assert.Equal([(1, 3034), (2, 3034)], order);
    }
}
