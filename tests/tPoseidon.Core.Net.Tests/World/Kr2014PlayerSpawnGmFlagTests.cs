using tPoseidon.Core.Data;
using tPoseidon.Kr2014.Wire;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class Kr2014PlayerSpawnGmFlagTests
{
    private const string Name = "Keeper";

    private const string BytesBeforeGmFlags =
        "01030100040002042500080000002C03C7012C03C7014B65657065720000802E000000000100000000000000000000000000040000000062000000D60B0000F71A0000291B0000000000000200000000000000000000000000000000000000000000000000000000";

    private static PlayerSpawnLoginPacket Packet(byte gmFlags) => new()
    {
        EntityId = 0x0301,
        BodyType = 4,
        Hair = 2,
        ClassChainId = 4,
        Level = 37,
        PositionX = 812,
        PositionY = 455,
        RespawnX = 812,
        RespawnY = 455,
        Name = Name,
        EncodedYaw = 0x80,
        SpeedQuarter = 0x2E,
        ClassChainMirror = 4,
        GmFlags = gmFlags,
        LookItems = new Dictionary<byte, uint>
        {
            [EquipmentSlot.Ear] = 6903,
            [EquipmentSlot.Tail] = 6953,
            [EquipmentSlot.Weapon] = 3030,
        },
    };

    private static int FlagsAt => PlayerSpawnLoginPacket.HeaderBytes + Name.Length + 1 + PlayerSpawnLoginPacket.GmFlagsOffset;

    [Fact]
    public void NonGmPlayerBytesAreUnchanged()
    {
        Assert.Equal(BytesBeforeGmFlags, Convert.ToHexString(Packet(0).EncodePayload()));
    }

    [Fact]
    public void GmEagleSetsOnlyTheFlagsByteInFrontOfTheClassChainMirror()
    {
        var plain = Packet(0).EncodePayload();
        var gm = Packet(PlayerSpawnLoginPacket.GmEagleFlag).EncodePayload();

        Assert.Equal(plain.Length, gm.Length);
        Assert.Equal(PlayerSpawnLoginPacket.GmEagleFlag, gm[FlagsAt]);
        for (var i = 0; i < gm.Length; i++)
        {
            if (i != FlagsAt) Assert.Equal(plain[i], gm[i]);
        }
        Assert.Equal(4u, BitConverter.ToUInt32(gm, FlagsAt + 1));
    }

    [Fact]
    public void FlagsRoundTrip()
    {
        const byte wings = PlayerSpawnLoginPacket.GmEagleFlag | PlayerSpawnLoginPacket.GmEagleWingsFlag;
        Assert.Equal(wings, PlayerSpawnLoginPacket.DecodePayload(Packet(wings).EncodePayload()).GmFlags);
    }
}
