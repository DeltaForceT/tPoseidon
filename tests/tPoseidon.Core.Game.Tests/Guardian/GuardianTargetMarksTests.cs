using tPoseidon.Core.Game.Guardian;

namespace tPoseidon.Core.Game.Tests.Guardian;

public sealed class GuardianTargetMarksTests
{
    [Fact]
    public void ANewMarkEnablesMarkingAndReplacesThePreviousTarget()
    {
        var marks = new GuardianTargetMarks();

        Assert.True(marks.Mark(10));
        Assert.True(marks.Enabled);
        Assert.True(marks.Mark(11));
        Assert.Equal(new ushort[] { 11 }, marks.Targets);
        Assert.Equal((ushort)11, marks.First);
    }

    [Fact]
    public void MarkingTheSameTargetAgainChangesNothing()
    {
        var marks = new GuardianTargetMarks();
        marks.Mark(10);

        Assert.False(marks.Mark(10));
        Assert.Equal(new ushort[] { 10 }, marks.Targets);
    }

    [Fact]
    public void ReleasingTheLastTargetDisablesMarking()
    {
        var marks = new GuardianTargetMarks();
        marks.Mark(10);

        Assert.False(marks.Release(99));
        Assert.True(marks.Enabled);
        Assert.True(marks.Release(10));
        Assert.False(marks.Enabled);
        Assert.Null(marks.First);
    }

    [Fact]
    public void ClearReportsWhetherAnythingWasMarked()
    {
        var marks = new GuardianTargetMarks();

        Assert.False(marks.Clear());
        marks.Mark(10);
        Assert.True(marks.Clear());
        Assert.False(marks.Enabled);
        Assert.Empty(marks.Targets);
    }
}
