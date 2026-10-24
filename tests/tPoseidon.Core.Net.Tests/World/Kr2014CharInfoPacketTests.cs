using tPoseidon.Core.Net.World;
using tPoseidon.Kr2014.Wire;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class Kr2014CharInfoPacketTests
{
    private static CharInfoPacket Sample() => new()
    {
        EquipSlots = new[] { new CharInfoPacket.EquipSlot(0x11223344, 1) },
        Stats = new CharacterStatBlock(
            new short[] { 18, 6, 6, 3, 3, 3, 12, 12, 12, 9, 9, 9 },
            BonusPoints: 12,
            TmPoint: 2,
            TmLevel: 3,
            Level: 4,
            CharacterStatBlock.LoginMarkAsStored),
        MapId = 33,
        PositionX = 1144,
        PositionY = 1304,
        ReturnMapId = 148,
        Money = 2000,
        Hp = 150,
        Mp = 60,
        TmExp = 256,
        Exp = 40,
        Hair = 10,
    };

    [Fact]
    public void ThePayloadIsThe23SlotsTheStatBlockAndTheStateRecord()
    {
        var payload = Sample().EncodePayload();

        Assert.Equal(CharInfoPacket.PayloadSize, payload.Length);
        Assert.Equal(0xB8, CharInfoPacket.StatBlockOffset);
        Assert.Equal(0xD9, CharInfoPacket.StateRecordOffset);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11, 0x01, 0x00, 0x00, 0x00 }, payload[..8]);
        Assert.All(payload[8..0xB8], b => Assert.Equal(0, b));
    }

    [Fact]
    public void TheStatBlockCarriesPointsThenBonusTmPointsTmLevelAndLevel()
    {
        var payload = Sample().EncodePayload();

        Assert.Equal(
            new byte[]
            {
                18, 0, 6, 0, 6, 0, 3, 0, 3, 0, 3, 0, 12, 0, 12, 0, 12, 0, 9, 0, 9, 0, 9, 0,
                12, 0, 2, 0, 3, 0, 4, 0, 0,
            },
            payload[0xB8..0xD9]);
    }

    [Fact]
    public void TheStateRecordCarriesHpAndMpAsOne32BitValueEach()
    {
        var payload = Sample().EncodePayload();

        Assert.Equal(new byte[] { 0x01, 0x00 }, payload[0xD9..0xDB]);
        Assert.Equal(new byte[] { 0x21, 0x00 }, payload[0xDB..0xDD]);
        Assert.Equal(new byte[] { 0x78, 0x04, 0x00, 0x00 }, payload[0xDD..0xE1]);
        Assert.Equal(new byte[] { 0x18, 0x05, 0x00, 0x00 }, payload[0xE1..0xE5]);
        Assert.Equal(new byte[] { 0x94, 0x00 }, payload[0xE5..0xE7]);
        Assert.Equal(new byte[] { 0xD0, 0x07, 0x00, 0x00 }, payload[0xE7..0xEB]);
        Assert.Equal(new byte[] { 0x96, 0x00, 0x00, 0x00 }, payload[0xEB..0xEF]);
        Assert.Equal(new byte[] { 0x3C, 0x00, 0x00, 0x00 }, payload[0xEF..0xF3]);
        Assert.Equal(new byte[] { 0x00, 0x01, 0x00, 0x00 }, payload[0xF3..0xF7]);
        Assert.Equal(new byte[] { 0x28, 0x00, 0x00, 0x00 }, payload[0xF7..0xFB]);
        Assert.All(payload[0xFB..0x10F], b => Assert.Equal(0, b));
        Assert.Equal(new byte[] { 10, 0, 1, 0x50, 0x00, 0x00, 0x00 }, payload[0x10F..0x116]);
    }
}
