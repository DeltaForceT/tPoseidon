using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GuardianGrowthFramesTests
{
    private static GuardianState State() => new(
        GuardianUid: 0x55,
        SlotNum: 0,
        GuardianType: 15001,
        Name: "Spirit",
        Level: 13,
        FType: 1,
        RareType: 2,
        TitleIndex: 3,
        PtsP: 4,
        PtsM: 3,
        PtsS: 3,
        PtsC: 3,
        StatPoints: new short[12],
        Exp: 0x01020304,
        Gauge: 0x0A0B0C0D,
        SkillPoint: 10,
        Skills: []);

    [Fact]
    public void LevelUpCarriesLevelExpAndSkillPointsAsThreeDwords()
    {
        Assert.Equal(
            new byte[] { 0x0D, 0, 0, 0, 0x04, 0x03, 0x02, 0x01, 0x0A, 0, 0, 0 },
            GuardianFrames.LevelUp(13, 0x01020304, 10));
    }

    [Fact]
    public void LevelUpEffectIsTheGuardianEntityId()
    {
        Assert.Equal(new byte[] { 0x34, 0x12 }, GuardianFrames.LevelUpEffect(0x1234));
    }

    [Fact]
    public void ExpGainedIsTheEntityIdThenTheGainedExp()
    {
        Assert.Equal(new byte[] { 0x34, 0x12, 0x19, 0, 0, 0 }, GuardianFrames.ExpGained(0x1234, 25));
    }

    [Fact]
    public void SpeechIsTheGuardianEntityIdThenTheNulTerminatedText()
    {
        Assert.Equal(
            new byte[] { 0x34, 0x12, (byte)'F', (byte)'u', (byte)'n', 0 },
            GuardianFrames.Speech(0x1234, "Fun", System.Text.Encoding.ASCII));
    }

    [Fact]
    public void MarksAreACountThenTheEntityIds()
    {
        Assert.Equal(new byte[] { 2, 0x0A, 0, 0x0B, 0x01 }, GuardianFrames.Marks([10, 0x010B]));
        Assert.Equal(new byte[] { 0 }, GuardianFrames.Marks([]));
    }

    [Fact]
    public void LockOnRequestIsOneEntityId()
    {
        Assert.True(GuardianFrames.TryReadLockOn([0x34, 0x12], out var target));
        Assert.Equal(0x1234, target);
        Assert.False(GuardianFrames.TryReadLockOn([0x34], out _));
    }

    [Fact]
    public void SpawnPutsTheGuardianTailAfterTheCharacterPartAndOwner()
    {
        var guardian = new GuardianActor { EntityId = 0x2222, GuardianUid = 0x55, MapId = 7, SpeedQuarter = 9 };
        var payload = GuardianFrames.Spawn(guardian, State(), 0x1111, 100, 200);

        Assert.Equal(0x2222, BitConverter.ToUInt16(payload, 0));
        Assert.Equal(0x10, payload[2]);
        Assert.Equal(15001, BitConverter.ToUInt16(payload, 4));
        var tail = payload.Length - GuardianFrames.SpawnTrailerSize;
        Assert.Equal(0x80, payload[tail + 1]);
        Assert.Equal(9, payload[tail + 2]);
        Assert.Equal(0x1111, BitConverter.ToUInt16(payload, tail + GuardianFrames.SpawnCharacterPartSize));
        var fields = tail + GuardianFrames.SpawnCharacterPartSize + 2;
        Assert.Equal(1, payload[fields]);
        Assert.Equal(0u, BitConverter.ToUInt32(payload, fields + 1));
        Assert.Equal(0x0A0B0C0D, BitConverter.ToInt32(payload, fields + 5));
        Assert.Equal(0x01020304, BitConverter.ToInt32(payload, fields + 9));
        Assert.Equal(2, payload[fields + 13]);
    }
}
