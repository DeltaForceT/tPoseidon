using tPoseidon.Kr2014.Wire;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class Kr2014PlayerSpawnTailTests
{
    private const string Name = "Tester";

    [Fact]
    public void TailStartsWithStateThenFacingThenSpeedQuarter()
    {
        var payload = new PlayerSpawnLoginPacket
        {
            EntityId = 0x0301,
            Name = Name,
            EncodedYaw = 0x80,
            SpeedQuarter = 0x2E,
        }.EncodePayload();

        var tail = PlayerSpawnLoginPacket.HeaderBytes + Name.Length + 1;
        Assert.Equal(0x00, payload[tail]);
        Assert.Equal(0x80, payload[tail + 1]);
        Assert.Equal(0x2E, payload[tail + 2]);
    }

    [Fact]
    public void SpeedQuarterDefaultsToZeroSoLoginAndLandingBodiesAreUnchanged()
    {
        var payload = new PlayerSpawnLoginPacket { EntityId = 0x0301, Name = Name }.EncodePayload();

        Assert.Equal(0x00, payload[PlayerSpawnLoginPacket.HeaderBytes + Name.Length + 1 + 2]);
    }
}
