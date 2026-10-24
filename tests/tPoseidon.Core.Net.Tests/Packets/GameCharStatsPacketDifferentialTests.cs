using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public sealed class GameCharStatsPacketDifferentialTests
{
    private static readonly string DataDir = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data");

    private static byte[] LoadFixture(string filename) =>
        File.ReadAllBytes(Path.Combine(DataDir, filename));

    [RequiresDataFact("_ref-respec-lion-lvl4.frame-op00E9.plain.bin")]
    public void RespecLionLvl4_DecodesAndRoundtrips()
    {
        var bytes = LoadFixture("_ref-respec-lion-lvl4.frame-op00E9.plain.bin");
        Assert.Equal(GameCharStatsPacket.PayloadSize, bytes.Length);

        var typed = GameCharStatsPacket.DecodePayload(bytes);

        Assert.Equal(4, typed.Level);
        Assert.Equal(148, typed.MapId);
        Assert.Equal(148, typed.MapIdMirror);
        Assert.Equal(18, typed.Ap);
        Assert.Equal(6,  typed.Ac);
        Assert.Equal(6,  typed.Dx);
        Assert.Equal(3,  typed.MaxMp);
        Assert.Equal(12, typed.MaxWt);
        Assert.Equal(9,  typed.MaxHp);
        Assert.Equal(180, typed.MaxHpDerived);
        Assert.Equal(80,  typed.MaxMpDerived);
        Assert.Equal(0u,  typed.Money);
        Assert.Equal(0,   typed.AttrPts);
        Assert.Equal(0x05C97380u, typed.EquipSlots[5].ItemUid);
        Assert.Equal(0x05C97381u, typed.EquipSlots[6].ItemUid);
        Assert.Equal(0x05C9738Eu, typed.EquipSlots[0].ItemUid);
        Assert.Equal(0x05C9738Du, typed.EquipSlots[1].ItemUid);
        Assert.Equal(0x05C9738Fu, typed.EquipSlots[2].ItemUid);
        Assert.Equal(256u, typed.TmExp);
        Assert.Equal(40u,  typed.CurrentLevelExp);

        Assert.Equal(bytes, typed.EncodePayload());
    }

    [RequiresDataFact("_ref-snapshot-progressed-lvl8.frame-op00E9.plain.bin")]
    public void SnapshotProgressedLvl8_DecodesAndRoundtrips()
    {
        var bytes = LoadFixture("_ref-snapshot-progressed-lvl8.frame-op00E9.plain.bin");
        Assert.Equal(GameCharStatsPacket.PayloadSize, bytes.Length);

        var typed = GameCharStatsPacket.DecodePayload(bytes);

        Assert.Equal(8,   typed.Level);
        Assert.Equal(148, typed.MapId);
        Assert.Equal(148, typed.MapIdMirror);
        Assert.Equal(28,  typed.Ap);
        Assert.Equal(28,  typed.Ac);
        Assert.Equal(28,  typed.Dx);
        Assert.Equal(7,   typed.MaxMp);
        Assert.Equal(28,  typed.MaxHp);
        Assert.Equal(330, typed.MaxHpDerived);
        Assert.Equal(110, typed.MaxMpDerived);
        Assert.Equal(2000u, typed.Money);
        Assert.Equal(28,  typed.AttrPts);
        Assert.Equal(3,   typed.TmLevel);
        Assert.Equal(3,   typed.TmPoint);
        Assert.Equal(200u, typed.TmExp);
        Assert.Equal(378u, typed.CurrentLevelExp);

        Assert.Equal(bytes, typed.EncodePayload());
    }
}
