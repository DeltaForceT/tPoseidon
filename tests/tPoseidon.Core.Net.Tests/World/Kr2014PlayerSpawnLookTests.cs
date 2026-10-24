using tPoseidon.Core.Data;
using tPoseidon.Kr2014.Wire;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class Kr2014PlayerSpawnLookTests
{
    private const string Name = "Tester";
    private const int Footer = PlayerSpawnLoginPacket.HeaderBytes + 6 + 1;

    private static uint U32(byte[] payload, int offset) => BitConverter.ToUInt32(payload, offset);

    [Fact]
    public void EarsAndTailAloneKeepTheCompactFooter()
    {
        var payload = new PlayerSpawnLoginPacket
        {
            EntityId = 0x0301,
            Name = Name,
            Hair = 7,
            LookItems = new Dictionary<byte, uint> { [EquipmentSlot.Ear] = 6901, [EquipmentSlot.Tail] = 6951 },
        }.EncodePayload();

        Assert.Equal(Footer + PlayerSpawnLoginPacket.CompactFooterBytes, payload.Length);
        Assert.Equal(0x00, payload[Footer + 25]);
        Assert.Equal(0x60u, U32(payload, Footer + 26));
        Assert.Equal(6901u, U32(payload, Footer + 30));
        Assert.Equal(6951u, U32(payload, Footer + 34));
        Assert.Equal(0u, U32(payload, Footer + 38));
        Assert.Equal(7, payload[Footer + 42]);
        Assert.All(payload[(Footer + 43)..], b => Assert.Equal(0, b));
    }

    [Fact]
    public void EarAndTailBitsAreSentEvenWithoutItems()
    {
        var payload = new PlayerSpawnLoginPacket { EntityId = 0x0301, Name = Name }.EncodePayload();

        Assert.Equal(Footer + PlayerSpawnLoginPacket.CompactFooterBytes, payload.Length);
        Assert.Equal(0x60u, U32(payload, Footer + 26));
        Assert.Equal(0u, U32(payload, Footer + 30));
        Assert.Equal(0u, U32(payload, Footer + 34));
    }

    [Fact]
    public void AWornWeaponAddsItsBitAndItsIdInBitOrder()
    {
        var payload = new PlayerSpawnLoginPacket
        {
            EntityId = 0x0301,
            Name = Name,
            Hair = 7,
            LookItems = new Dictionary<byte, uint>
            {
                [EquipmentSlot.Weapon] = 3030,
                [EquipmentSlot.Ear] = 6901,
                [EquipmentSlot.Tail] = 6951,
            },
        }.EncodePayload();

        Assert.Equal(Footer + PlayerSpawnLoginPacket.CompactFooterBytes + 4, payload.Length);
        Assert.Equal(0x62u, U32(payload, Footer + 26));
        Assert.Equal(3030u, U32(payload, Footer + 30));
        Assert.Equal(6901u, U32(payload, Footer + 34));
        Assert.Equal(6951u, U32(payload, Footer + 38));
        Assert.Equal(0u, U32(payload, Footer + 42));
        Assert.Equal(7, payload[Footer + 46]);
    }

    [Fact]
    public void CostumesDrillAndCapeUseTheirOwnBits()
    {
        var packet = new PlayerSpawnLoginPacket
        {
            EntityId = 0x0301,
            Name = Name,
            LookItems = new Dictionary<byte, uint>
            {
                [10] = 14001,
                [EquipmentSlot.Drill] = 8000,
                [EquipmentSlot.Cape] = 15000,
                [EquipmentSlot.Belt] = 9999,
            },
        };

        Assert.Equal(0x60u | (1u << 7) | (1u << 18) | (1u << 19), packet.LookMask);
        var payload = packet.EncodePayload();
        Assert.Equal(14001u, U32(payload, Footer + 38));
        Assert.Equal(8000u, U32(payload, Footer + 42));
        Assert.Equal(15000u, U32(payload, Footer + 46));
    }

    [Fact]
    public void ADecodedSpawnEncodesToTheSameBytes()
    {
        var payload = new PlayerSpawnLoginPacket
        {
            EntityId = 0x0301,
            Name = Name,
            Hair = 3,
            EncodedYaw = 0x80,
            SpeedQuarter = 0x2E,
            LookItems = new Dictionary<byte, uint>
            {
                [EquipmentSlot.Hat] = 4030,
                [EquipmentSlot.Shield] = 6530,
                [EquipmentSlot.Ear] = 6905,
                [EquipmentSlot.Tail] = 6955,
            },
        }.EncodePayload();

        var decoded = PlayerSpawnLoginPacket.DecodePayload(payload);

        Assert.Equal(4030u, decoded.LookItems[EquipmentSlot.Hat]);
        Assert.Equal(6530u, decoded.LookItems[EquipmentSlot.Shield]);
        Assert.Equal(payload, decoded.EncodePayload());
    }
}
