using tPoseidon.Kr2014.Wire;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class Kr2014PlayerSpawnPostureTests
{
    private const string Name = "Keeper";

    private static PlayerSpawnLoginPacket Packet(byte postureCode) => new()
    {
        EntityId = 0x0301,
        BodyType = 4,
        Name = Name,
        PostureCode = postureCode,
    };

    [Fact]
    public void ThePostureCodeIsTheFirstFooterByteAfterTheName()
    {
        var payload = Packet(0x0D).EncodePayload();

        Assert.Equal(0x0D, payload[PlayerSpawnLoginPacket.HeaderBytes + Name.Length + 1]);
    }

    [Fact]
    public void AStandingPlayerKeepsTheFooterByteZero()
    {
        var payload = Packet(0).EncodePayload();

        Assert.Equal(0, payload[PlayerSpawnLoginPacket.HeaderBytes + Name.Length + 1]);
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)2)]
    [InlineData((byte)0x0D)]
    public void ThePostureCodeRoundTrips(byte code)
    {
        var decoded = PlayerSpawnLoginPacket.DecodePayload(Packet(code).EncodePayload());

        Assert.Equal(code, decoded.PostureCode);
    }
}
