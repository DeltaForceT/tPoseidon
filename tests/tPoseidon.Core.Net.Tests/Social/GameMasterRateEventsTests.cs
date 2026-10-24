using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GameMasterRateEventsTests
{
    private const long Start = 1_800_000_000;
    private const long End = Start + 3600;

    private static readonly Encoding Text = Encoding.Latin1;
    private static readonly string[] ReserveArguments = { "100", "50", "1800000000", "1800003600" };
    private static readonly string[] BackwardsArguments = { "100", "50", "1800003600", "1800000000" };
    private static readonly string[] NowArguments = { "50", "0" };

    private static readonly GameMasterExpEvent Event = new(100, 50, Start, End);

    [Fact]
    public void ExpEventIsTwentyBytesInWireOrder()
    {
        var w = new PacketWriter();
        Event.WriteTo(w);
        var bytes = w.ToArray();

        Assert.Equal(GameMasterExpEvent.Length, bytes.Length);
        Assert.Equal(new byte[] { 100, 0, 50, 0 }, bytes.AsSpan(0, 4).ToArray());
        Assert.Equal(Start, BitConverter.ToInt64(bytes, 4));
        Assert.Equal(End, BitConverter.ToInt64(bytes, 12));
        Assert.True(GameMasterExpEvent.TryRead(bytes, out var read));
        Assert.Equal(Event, read);
        Assert.False(GameMasterExpEvent.TryRead(bytes.AsSpan(0, GameMasterExpEvent.Length - 1), out _));
    }

    [Fact]
    public void ScheduleMovesAnEventToRunningAtItsStartAndExpiresItAtItsEnd()
    {
        var schedule = new GameMasterSchedule<GameMasterExpEvent>();
        schedule.Reserve(Event);

        var before = schedule.Tick(Start - 1);
        Assert.Null(before.Applied);
        Assert.Single(schedule.Waiting);

        var started = schedule.Tick(Start);
        Assert.Equal(Event, started.Applied);
        Assert.Null(started.Expired);
        Assert.Empty(schedule.Waiting);
        Assert.Equal(Event, schedule.Current);

        Assert.Null(schedule.Tick(Start + 10).Applied);

        var ended = schedule.Tick(End);
        Assert.Equal(Event, ended.Expired);
        Assert.Equal(default(GameMasterExpEvent), ended.Applied);
        Assert.Empty(schedule.Running);
    }

    [Fact]
    public void ScheduleMovesOneDueEventPerTickAndAppliesTheFirstRunningOne()
    {
        var schedule = new GameMasterSchedule<GameMasterExpEvent>();
        var second = new GameMasterExpEvent(200, 0, Start, End);
        schedule.Reserve(Event);
        schedule.Reserve(second);

        Assert.Equal(Event, schedule.Tick(Start).Applied);
        Assert.Single(schedule.Waiting);
        Assert.Null(schedule.Tick(Start).Applied);
        Assert.Equal(new[] { Event, second }, schedule.Running);
        Assert.Equal(Event, schedule.Current);
    }

    [Fact]
    public void DeleteRemovesAnEqualEventOnly()
    {
        var schedule = new GameMasterSchedule<GameMasterExpEvent>();
        schedule.Reserve(Event);

        Assert.False(schedule.Delete(Event with { TmPercent = 51 }));
        Assert.True(schedule.Delete(Event));
        Assert.Empty(schedule.Waiting);
    }

    [Fact]
    public void ListReplyCarriesTheTimeTheAppliedEventAndBothLists()
    {
        var control = new GameMasterRateControl(new MemoryRateStore());
        control.ReserveExp(Event);
        var time = new DateTime(2026, 10, 24, 20, 5, 6, 7, DateTimeKind.Local);

        var body = control.ExpListReply(time);

        Assert.Equal(GameMasterEventFrames.SystemTimeLength + GameMasterExpEvent.Length + 2 + 2 + GameMasterExpEvent.Length, body.Length);
        Assert.Equal(2026, BitConverter.ToUInt16(body, 0));
        Assert.Equal((ushort)time.DayOfWeek, BitConverter.ToUInt16(body, 4));
        Assert.Equal(7, BitConverter.ToUInt16(body, 14));
        Assert.True(GameMasterEventFrames.TryReadExpList(body, withTime: true, withCurrent: true, out var list));
        Assert.Equal(time, list.Time);
        Assert.Equal(default(GameMasterExpEvent), list.Current);
        Assert.Empty(list.Running);
        Assert.Equal(new[] { Event }, list.Waiting);
    }

    [Fact]
    public void ReserveRepliesWithTheListsAndDeleteWithTheAppliedEventToo()
    {
        var control = new GameMasterRateControl(new MemoryRateStore());

        var reserved = control.ReserveExp(Event);
        Assert.Equal(2 + 2 + GameMasterExpEvent.Length, reserved.Length);

        var deleted = control.DeleteExp(Event);
        Assert.Equal(GameMasterExpEvent.Length + 2 + 2, deleted.Length);
        Assert.True(GameMasterEventFrames.TryReadExpList(deleted, withTime: false, withCurrent: true, out var list));
        Assert.Empty(list.Waiting);
    }

    [Fact]
    public void TickSendsTheAppliedPercentagesToTheGameServers()
    {
        var control = new GameMasterRateControl(new MemoryRateStore());
        control.ReserveExp(Event);

        Assert.Empty(control.Tick(Start - 1));
        var applied = Assert.Single(control.Tick(Start));
        Assert.Equal(GameMasterEventFrames.ExpApply, applied.Opcode);
        Assert.Equal(new byte[] { 100, 0, 50, 0 }, applied.Body);
        Assert.Equal(new byte[] { 100, 0, 50, 0 }, Assert.Single(control.LinkState()).Body);

        var cleared = Assert.Single(control.Tick(End));
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, cleared.Body);
        Assert.Empty(control.LinkState());
    }

    [Fact]
    public void ExpNowIsWhatTheQueryReports()
    {
        var control = new GameMasterRateControl(new MemoryRateStore());

        var message = control.SetExpNow(30, 20);

        Assert.Equal(GameMasterEventFrames.ExpApply, message.Opcode);
        Assert.Equal(new byte[] { 30, 0, 20, 0 }, control.ExpQueryReply());
        control.ReserveExp(Event);
        control.Tick(Start);
        Assert.Equal(new byte[] { 30, 0, 20, 0 }, control.ExpQueryReply());
    }

    [Theory]
    [InlineData(1000, 100, 2000)]
    [InlineData(1000, 50, 1500)]
    [InlineData(1000, 0, 1000)]
    [InlineData(40000, 0, 40000)]
    [InlineData(7, 10, 7)]
    [InlineData(0, 100, 0)]
    public void ScaleAddsTheTruncatedSlotShare(int gain, ushort percent, int expected)
    {
        Assert.Equal(expected, GameMasterRates.Scale(gain, percent));
    }

    [Fact]
    public void RatesChangeOnlyWhenTheValuesDiffer()
    {
        var rates = new GameMasterRates();

        Assert.False(rates.HasExp);
        Assert.True(rates.SetExp(100, 0));
        Assert.False(rates.SetExp(100, 0));
        Assert.True(rates.HasExp);
        Assert.Equal(2000, rates.ScaleExp(1000));
        Assert.Equal(1000, rates.ScaleTm(1000));
        Assert.Equal(0f, rates.TmSlotRate);
        Assert.Equal(1.00005f, rates.ExpSlotRate, 5);
    }

    [Fact]
    public void ToolBuildsTheExpCommands()
    {
        Assert.Equal(GameMasterEventFrames.ExpList, GameMasterToolCommands.Build("exp-list", Array.Empty<string>(), Text)!.Opcode);
        var reserve = GameMasterToolCommands.Build("exp-reserve", ReserveArguments, Text)!;
        Assert.Equal(GameMasterEventFrames.ExpReserve, reserve.Opcode);
        Assert.True(GameMasterExpEvent.TryRead(reserve.Body, out var reserved));
        Assert.Equal(Event, reserved);
        Assert.Equal(GameMasterEventFrames.ExpDelete, GameMasterToolCommands.Build("exp-delete", ReserveArguments, Text)!.Opcode);
        Assert.Null(GameMasterToolCommands.Build("exp-reserve", BackwardsArguments, Text));
        var now = GameMasterToolCommands.Build("exp-now", NowArguments, Text)!;
        Assert.Equal(GameMasterEventFrames.ExpApply, now.Opcode);
        Assert.Equal(new byte[] { 50, 0, 0, 0 }, now.Body);
        Assert.Equal(GameMasterEventFrames.ExpQuery, GameMasterToolCommands.Build("exp-get", Array.Empty<string>(), Text)!.Opcode);
    }

    [Fact]
    public void ToolReadsLocalTimes()
    {
        Assert.True(GameMasterToolCommands.TryParseTime("2026-10-24T20:00", out var parsed));
        Assert.Equal(new DateTimeOffset(new DateTime(2026, 10, 24, 20, 0, 0, DateTimeKind.Local)).ToUnixTimeSeconds(), parsed);
        Assert.False(GameMasterToolCommands.TryParseTime("soon", out _));
    }

    [Fact]
    public void ToolDescribesTheExpReplies()
    {
        var control = new GameMasterRateControl(new MemoryRateStore());
        control.ReserveExp(Event);

        var listed = GameMasterToolConnection.Describe(new ToolReply(GameMasterEventFrames.ExpListReply, control.ExpListReply(DateTime.Now)));
        Assert.Contains("applied now: none", listed, StringComparison.Ordinal);
        Assert.Contains("EXP +100%, TM +50%", listed, StringComparison.Ordinal);
        Assert.Contains("running: none", listed, StringComparison.Ordinal);
        Assert.Equal("set with exp-now: EXP +0%, TM +0%", GameMasterToolConnection.Describe(new ToolReply(GameMasterEventFrames.ExpQueryReply, control.ExpQueryReply())));
    }

    [Fact]
    public void AbilityEventIsNineteenBytesInWireOrder()
    {
        var w = new PacketWriter();
        Ability.WriteTo(w);
        var bytes = w.ToArray();

        Assert.Equal(GameMasterAbilityEvent.Length, bytes.Length);
        Assert.Equal(new byte[] { 9, 20, 0 }, bytes.AsSpan(0, 3).ToArray());
        Assert.Equal(Start, BitConverter.ToInt64(bytes, 3));
        Assert.Equal(End, BitConverter.ToInt64(bytes, 11));
        Assert.True(GameMasterAbilityEvent.TryRead(bytes, out var read));
        Assert.Equal(Ability, read);
    }

    [Fact]
    public void AbilityListReplyHasTheTimeAndBothListsButNoAppliedEvent()
    {
        var control = new GameMasterRateControl(new MemoryRateStore());

        var reserved = control.ReserveAbility(Ability);
        Assert.Equal(2 + 2 + GameMasterAbilityEvent.Length, reserved.Length);

        var listed = control.AbilityListReply(DateTime.Now);
        Assert.Equal(GameMasterEventFrames.SystemTimeLength + 2 + 2 + GameMasterAbilityEvent.Length, listed.Length);
        Assert.True(GameMasterEventFrames.TryReadAbilityList(listed, withTime: true, out var list));
        Assert.Equal(new[] { Ability }, list.Waiting);
    }

    [Fact]
    public void AbilityTickAppliesThePercentAndClearsTheStatWhenItEnds()
    {
        var control = new GameMasterRateControl(new MemoryRateStore());
        control.ReserveAbility(Ability);

        var applied = Assert.Single(control.Tick(Start));
        Assert.Equal(GameMasterEventFrames.AbilityApply, applied.Opcode);
        Assert.Equal(new byte[] { 9, 20, 0 }, applied.Body);
        Assert.Equal(new byte[] { 9, 20, 0 }, Assert.Single(control.LinkState()).Body);

        var ended = control.Tick(End);
        Assert.Equal(2, ended.Count);
        Assert.Equal(new byte[] { 9, 0, 0 }, ended[0].Body);
        Assert.Equal(new byte[] { 0, 0, 0 }, ended[1].Body);
        Assert.Empty(control.LinkState());
    }

    [Fact]
    public void AbilitiesRaiseTheStatTableValue()
    {
        var rates = new GameMasterRates();
        Assert.False(rates.SetAbility(GameMasterRates.AbilitySlots, 10));
        Assert.True(rates.SetAbility(9, 20));
        Assert.False(rates.SetAbility(9, 20));

        var table = new tPoseidon.Core.Game.Characters.EffectiveStats(10, 20, 30, 40, 50, 60, 70, 80, 90, 1001, 110, 120);
        var raised = rates.WithAbilities(table);

        Assert.Equal(table with { Hp = 1201 }, raised);
        var packet = rates.AbilityPacket();
        Assert.Equal(GameMasterRates.StatSlots * 2, packet.Length);
        Assert.Equal(20, BitConverter.ToUInt16(packet, 9 * 2));
    }

    [Fact]
    public void ToolBuildsTheAbilityCommands()
    {
        Assert.Equal(GameMasterEventFrames.AbilityList, GameMasterToolCommands.Build("ability-list", Array.Empty<string>(), Text)!.Opcode);
        var reserve = GameMasterToolCommands.Build("ability-reserve", AbilityArguments, Text)!;
        Assert.Equal(GameMasterEventFrames.AbilityReserve, reserve.Opcode);
        Assert.True(GameMasterAbilityEvent.TryRead(reserve.Body, out var reserved));
        Assert.Equal(Ability, reserved);
        Assert.Equal(GameMasterEventFrames.AbilityDelete, GameMasterToolCommands.Build("ability-delete", AbilityArguments, Text)!.Opcode);
        Assert.Null(GameMasterToolCommands.Build("ability-reserve", BadStatArguments, Text));
        var listed = GameMasterToolConnection.Describe(new ToolReply(GameMasterEventFrames.AbilityReserved, new GameMasterRateControl(new MemoryRateStore()).ReserveAbility(Ability)));
        Assert.Contains("HP +20%", listed, StringComparison.Ordinal);
    }

    [Fact]
    public void SchedulesAndTheDirectPairSurviveARestart()
    {
        var store = new MemoryRateStore();
        var first = new GameMasterRateControl(store);
        first.ReserveExp(Event);
        first.ReserveExp(Event with { ExpPercent = 30, StartTime = End, EndTime = End + 60 });
        first.ReserveAbility(Ability);
        first.SetExpNow(50, 0);
        Assert.NotEmpty(first.Tick(Start));

        var restarted = new GameMasterRateControl(store);

        Assert.Equal(new[] { Event }, restarted.Exp.Running);
        Assert.Equal(new[] { Event with { ExpPercent = 30, StartTime = End, EndTime = End + 60 } }, restarted.Exp.Waiting);
        Assert.Equal(new[] { Ability }, restarted.Abilities.Running);
        Assert.Equal(GameMasterEventFrames.ExpPair(50, 0), restarted.ExpQueryReply());
        var reapplied = restarted.Tick(Start + 1);
        Assert.Contains(reapplied, message => message.Opcode == GameMasterEventFrames.ExpApply && message.Body.SequenceEqual(GameMasterEventFrames.ExpPair(100, 50)));
        Assert.Contains(reapplied, message => message.Opcode == GameMasterEventFrames.AbilityApply && message.Body.SequenceEqual(GameMasterEventFrames.AbilityChange(9, 20)));
    }

    [Fact]
    public void ADeletedEventIsGoneAfterARestart()
    {
        var store = new MemoryRateStore();
        var first = new GameMasterRateControl(store);
        first.ReserveExp(Event);
        first.DeleteExp(Event);

        Assert.Empty(new GameMasterRateControl(store).Exp.Waiting);
    }

    private sealed class MemoryRateStore : IGmRateEventStore
    {
        private GmRateEventState _state = GmRateEventState.Empty;

        public GmRateEventState Load() => _state;

        public void Save(GmRateEventState state) => _state = state;
    }

    private static readonly GameMasterAbilityEvent Ability = new(9, 20, Start, End);
    private static readonly string[] AbilityArguments = { "hp", "20", "1800000000", "1800003600" };
    private static readonly string[] BadStatArguments = { "13", "20", "1800000000", "1800003600" };
}
