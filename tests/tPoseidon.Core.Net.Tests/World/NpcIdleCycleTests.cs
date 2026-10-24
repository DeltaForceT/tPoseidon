using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class NpcIdleCycleTests
{
    private const int ChoiceFace135 = 0;
    private const int ChoiceFace225 = 1;
    private const int ChoiceMotion = 2;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly NpcEntity Npc = new()
    {
        EntityId = 0x0101,
        MapId = 1,
        ChrType = 1002,
        PosId = 1,
        Spawn = NpcPoint.Xy(100, 100),
    };

    private static readonly NpcActionScriptEntry TwoStepScript = new()
    {
        Type = 1002,
        MaxCnt = 2,
        Steps = new[]
        {
            new NpcActionScriptStep(Action: 2, Loop: 1, AniTime: 5, Speech: "first"),
            new NpcActionScriptStep(Action: 3, Loop: 0, AniTime: 3, Speech: string.Empty),
        },
    };

    private static NpcIdleCycle CycleWith(NpcActionScriptEntry? script, params int[] draws)
        => new(_ => new NpcIdleProfile(script, NpcIdleProfiles.WaitWithoutMotion), new ScriptedRandom(draws));

    private static DateTimeOffset At(int milliseconds) => Start + TimeSpan.FromMilliseconds(milliseconds);

    [Fact]
    public void FirstTick_StartsTheIdleWaitWithoutActing()
    {
        var cycle = CycleWith(TwoStepScript, 4000);

        Assert.Empty(cycle.Tick(new[] { Npc }, Start));
    }

    [Fact]
    public void NothingHappensBeforeTheIdleWaitEnds()
    {
        var cycle = CycleWith(TwoStepScript, 4000);
        cycle.Tick(new[] { Npc }, Start);

        Assert.Empty(cycle.Tick(new[] { Npc }, At(3999)));
    }

    [Theory]
    [InlineData(ChoiceFace135, NpcIdleCycle.DirectionAt135Degrees)]
    [InlineData(ChoiceFace225, NpcIdleCycle.DirectionAt225Degrees)]
    public void FacingChoice_TurnsTheNpc(int choice, byte expectedDirection)
    {
        var cycle = CycleWith(TwoStepScript, 4000, choice, 4000);
        cycle.Tick(new[] { Npc }, Start);

        var events = cycle.Tick(new[] { Npc }, At(4000));

        var facing = Assert.IsType<NpcFacingEvent>(Assert.Single(events));
        Assert.Equal(Npc.EntityId, facing.EntityId);
        Assert.Equal(expectedDirection, facing.Direction);
    }

    [Fact]
    public void TheSameFacingCanBeChosenTwiceInARow()
    {
        var cycle = CycleWith(TwoStepScript, 4000, ChoiceFace135, 4000, ChoiceFace135, 4000);
        cycle.Tick(new[] { Npc }, Start);

        var first = cycle.Tick(new[] { Npc }, At(4000));
        var second = cycle.Tick(new[] { Npc }, At(8000));

        Assert.Equal(NpcIdleCycle.DirectionAt135Degrees, Assert.IsType<NpcFacingEvent>(Assert.Single(first)).Direction);
        Assert.Equal(NpcIdleCycle.DirectionAt135Degrees, Assert.IsType<NpcFacingEvent>(Assert.Single(second)).Direction);
    }

    [Fact]
    public void MotionChoice_PlaysARandomStepOfTheScript()
    {
        var cycle = CycleWith(TwoStepScript, 4000, ChoiceMotion, 1);
        cycle.Tick(new[] { Npc }, Start);

        var events = cycle.Tick(new[] { Npc }, At(4000));

        var motion = Assert.IsType<NpcMotionEvent>(Assert.Single(events));
        Assert.Equal(Npc.EntityId, motion.EntityId);
        Assert.Equal(TwoStepScript.Steps[1], motion.Step);
    }

    [Fact]
    public void AfterAMotion_TheNpcWaitsForTheAnimationThenIdlesAgain()
    {
        var cycle = CycleWith(TwoStepScript, 4000, ChoiceMotion, 0, 4000, ChoiceFace225, 4000);
        cycle.Tick(new[] { Npc }, Start);
        cycle.Tick(new[] { Npc }, At(4000));

        Assert.Empty(cycle.Tick(new[] { Npc }, At(8999)));
        Assert.Empty(cycle.Tick(new[] { Npc }, At(9000)));
        Assert.Empty(cycle.Tick(new[] { Npc }, At(12999)));
        Assert.IsType<NpcFacingEvent>(Assert.Single(cycle.Tick(new[] { Npc }, At(13000))));
    }

    [Fact]
    public void MotionChoiceWithoutAScript_SendsNothingAndWaits()
    {
        var cycle = CycleWith(null, 4000, ChoiceMotion, 4000, ChoiceFace135, 4000);
        cycle.Tick(new[] { Npc }, Start);

        Assert.Empty(cycle.Tick(new[] { Npc }, At(4000)));
        Assert.Empty(cycle.Tick(new[] { Npc }, At(13999)));
        Assert.Empty(cycle.Tick(new[] { Npc }, At(14000)));
        Assert.IsType<NpcFacingEvent>(Assert.Single(cycle.Tick(new[] { Npc }, At(18000))));
    }

    [Fact]
    public void ForgottenNpcStartsOver()
    {
        var cycle = CycleWith(TwoStepScript, 4000, 4000);
        cycle.Tick(new[] { Npc }, Start);
        cycle.Forget(Npc.EntityId);

        Assert.Empty(cycle.Tick(new[] { Npc }, At(4000)));
    }

    [Fact]
    public void IdleWaitIsDrawnFromFourToSevenSeconds()
    {
        var random = new ScriptedRandom(4000);
        var cycle = new NpcIdleCycle(_ => new NpcIdleProfile(null, NpcIdleProfiles.WaitWithoutMotion), random);

        cycle.Tick(new[] { Npc }, Start);

        Assert.Equal((NpcIdleCycle.IdleWaitMinMs, NpcIdleCycle.IdleWaitMaxMs), Assert.Single(random.RangeRequests));
        Assert.Equal(4000, NpcIdleCycle.IdleWaitMinMs);
        Assert.Equal(7000, NpcIdleCycle.IdleWaitMaxMs);
    }

    private sealed class ScriptedRandom : Random
    {
        private readonly Queue<int> _draws;

        public ScriptedRandom(params int[] draws)
        {
            _draws = new Queue<int>(draws);
        }

        public List<(int Min, int Max)> RangeRequests { get; } = new();

        public override int Next(int maxValue) => _draws.Dequeue();

        public override int Next(int minValue, int maxValue)
        {
            RangeRequests.Add((minValue, maxValue));
            return _draws.Dequeue();
        }
    }
}
