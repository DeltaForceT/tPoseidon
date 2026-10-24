using tPoseidon.Core.Data;
using tPoseidon.Core.Game.SoulSeed;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SoulSeedFramesTests
{
    private static readonly short[] Points = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly short[] Session = [0, 0, 0, 0, 0, 0, 0, 7];
    private static readonly byte[] PlantedHead = [1, 1, 0x70, 0x94, 0, 0];
    private static readonly int[] KillPoints = [2, 1, 0, 0];

    private static readonly SoulSeedRecord Record = new(true, 38000, DateTimeOffset.FromUnixTimeSeconds(1000), DateTimeOffset.FromUnixTimeSeconds(2000), Points);

    [Fact]
    public void PlantedIsFlagThenTheThirtySevenByteRecord()
    {
        var frame = SoulSeedFrames.Planted(Record);
        Assert.Equal(1 + SoulSeedFrames.RecordSize, frame.Length);
        Assert.Equal(PlantedHead, frame[..6]);
        Assert.Equal(1000, BitConverter.ToInt64(frame, 6));
        Assert.Equal(2000, BitConverter.ToInt64(frame, 14));
        Assert.Equal(8, BitConverter.ToInt16(frame, 1 + SoulSeedFrames.RecordSize - 2));
    }

    [Fact]
    public void StateCarriesRecordSessionPointsAndCounters()
    {
        var counters = new short[SoulSeedRules.EventTypeCount];
        counters[54] = 9;
        var frame = SoulSeedFrames.State(Record, Session, counters);
        Assert.Equal(1 + SoulSeedFrames.RecordSize + SoulSeedFrames.SessionSize, frame.Length);
        Assert.Equal(7, BitConverter.ToInt16(frame, 1 + SoulSeedFrames.RecordSize + 14));
        Assert.Equal(9, BitConverter.ToInt16(frame, frame.Length - 2));
    }

    [Fact]
    public void BuffAndProgressLayouts()
    {
        var buff = SoulSeedFrames.Buff(new SoulSeedBuff(3, 600000, 2, new int[12]));
        Assert.Equal(SoulSeedFrames.BuffSize, buff.Length);
        Assert.Equal((3, 600000, 2), (BitConverter.ToInt32(buff, 0), BitConverter.ToInt32(buff, 4), BitConverter.ToInt32(buff, 8)));

        var row = new SoulSeedEventEntry(11, 10, 0, 0, false, 5, 5, KillPoints);
        var waiting = SoulSeedFrames.Progress(false, 3, row);
        Assert.Equal(5 + SoulSeedFrames.EventRowSize, waiting.Length);
        Assert.Equal(3, BitConverter.ToInt32(waiting, 1));
        Assert.Equal(5, BitConverter.ToInt32(waiting, 5 + SoulSeedFrames.EventRowRateOffset));
        Assert.Equal(2, BitConverter.ToInt32(waiting, 5 + SoulSeedFrames.EventRowPointsOffset));
        Assert.Equal(1 + SoulSeedFrames.EventRowSize, SoulSeedFrames.Progress(true, 0, row).Length);
    }

    [Fact]
    public void MasterMessagesRoundTrip()
    {
        Assert.Equal(new SoulSeedUseRequest(38000, 77, 2), SoulSeedMessages.DecodeUse(SoulSeedMessages.EncodeUse(new SoulSeedUseRequest(38000, 77, 2))));
        var state = new SoulSeedState(38001, DateTimeOffset.FromUnixTimeSeconds(5), DateTimeOffset.FromUnixTimeSeconds(6), Points);
        var decoded = SoulSeedMessages.DecodeState(SoulSeedMessages.EncodeState(state))!;
        Assert.Equal((state.SeedItemIndex, state.StartAt, state.LastUpdateAt), (decoded.SeedItemIndex, decoded.StartAt, decoded.LastUpdateAt));
        Assert.Equal(state.Points, decoded.Points);
        Assert.Null(SoulSeedMessages.DecodeState(SoulSeedMessages.EncodeState(null)));
        var awakening = new GuardianAwakening(9, "Reference_", 15000, 0, 2, 0, 4, 3, 2, 1, 8003);
        Assert.Equal(awakening, SoulSeedMessages.DecodeAwaken(9, SoulSeedMessages.EncodeAwaken(awakening)));
        Assert.Equal(new GuardianAwakened(12, 1), SoulSeedMessages.DecodeAwakened(SoulSeedMessages.EncodeAwakened(new GuardianAwakened(12, 1))));
        Assert.Equal(8003, SoulSeedMessages.DecodeReset(SoulSeedMessages.EncodeReset(8003)));
    }
}
