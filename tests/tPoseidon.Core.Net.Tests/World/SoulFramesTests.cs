using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SoulFramesTests
{
    private static readonly byte[] StatusWithoutPotion =
    [
        0x10, 0x27, 0, 0,
        7, 0, 0, 0, 0, 0, 0, 0,
        33, 0, 100, 0,
        0x55, 0, 0, 0, 1, 0, 0, 0,
        0, 0, 0, 0,
        3, 0,
        114, 0, 0, 0,
        5, 0,
        0,
        0,
    ];

    private static readonly byte[] OneInstance =
    [
        0x10, 0x27, 0, 0,
        1, 0,
        0x22, 0, 0, 0, 1, 0, 0, 0,
        0x6D, 0xC3, 0, 0,
        0x11, 0x60, 0x6D, 0x2C, 0x01, 0, 0, 0, 0, 0,
    ];

    [Fact]
    public void ManageStatusCarriesEveryFieldInWireOrder()
    {
        var status = new SoulManageStatus(10000, 7, 33, 100, 0x1_0000_0055, 3, 114, 5, 0);
        Assert.Equal(StatusWithoutPotion, SoulFrames.ManageStatus(status, null));
        var potion = new BagOperation(QuestResultFrames.Created, 1, 114, 0x1_0000_0066, 5);
        var withPotion = SoulFrames.ManageStatus(status, potion);
        Assert.Equal(1, withPotion[StatusWithoutPotion.Length - 1]);
        Assert.Equal(QuestResultFrames.ItemOperation(potion), withPotion[StatusWithoutPotion.Length..]);
    }

    [Fact]
    public void ItemInstancesAreTwentyTwoBytes()
    {
        var instance = new SoulItemInstance(0x1_0000_0022, 50029, new ItemProperty(0x11, 28000, 300, 0));
        Assert.Equal(OneInstance, SoulFrames.Charged(10000, [instance]));
        Assert.Equal(2 + 22, SoulFrames.PropertyUpdates([instance]).Length);
    }

    [Fact]
    public void RequestsReadTheirFixedLayouts()
    {
        byte[] register = [0x55, 0, 0, 0, 1, 0, 0, 0, 3, 0];
        Assert.True(SoulFrames.TryReadRegister(register, out var source, out var count));
        Assert.Equal((0x1_0000_0055UL, (ushort)3), (source, count));
        Assert.False(SoulFrames.TryReadRegister(register.AsSpan(0, 9), out _, out _));
        byte[] charge = [0x22, 0, 0, 0, 1, 0, 0, 0, 0x64, 0, 0, 0];
        Assert.True(SoulFrames.TryReadCharge(charge, out var item, out var amount));
        Assert.Equal((0x1_0000_0022UL, 100u), (item, amount));
        var craft = new byte[44];
        craft[0] = 0x6D;
        craft[1] = 0xC3;
        craft[4] = 0x11;
        Assert.True(SoulFrames.TryReadCraft(craft, out var result, out var materials));
        Assert.Equal(50029u, result);
        Assert.Equal(5, materials.Length);
        Assert.Equal(0x11UL, materials[0]);
        Assert.False(SoulFrames.TryReadCraft(craft.AsSpan(0, 43), out _, out _));
        Assert.True(SoulFrames.TryReadReinforce(new byte[48], out _, out var reinforceMaterials));
        Assert.Equal(5, reinforceMaterials.Length);
    }

    [Fact]
    public void ResultsLeadWithFlagOrSoul()
    {
        var removed = new BagOperation(QuestResultFrames.Removed, 1, 64816, 0x1_0000_0001, 0);
        var crafted = SoulFrames.Crafted(true, 500, [removed]);
        Assert.Equal(1, crafted[0]);
        Assert.Equal(500u, BitConverter.ToUInt32(crafted, 1));
        Assert.Equal(QuestResultFrames.ItemOperations([removed]), crafted[5..]);
        var used = SoulFrames.PotionUsed(600, [removed]);
        Assert.Equal(600u, BitConverter.ToUInt32(used, 0));
        var reinforced = SoulFrames.Reinforced(700, [removed], []);
        Assert.Equal(0, BitConverter.ToUInt16(reinforced, reinforced.Length - 2));
    }

    [Fact]
    public void SoulMessagesRoundTrip()
    {
        var row = new GuardianSoulRow(7, 33, 0x55, 3, 5, DateTimeOffset.FromUnixTimeMilliseconds(1_760_000_000_000));
        var account = new SoulAccount(10000, [row]);
        var decoded = SoulMessages.DecodeAccount(SoulMessages.EncodeAccount(account));
        Assert.Equal(10000u, decoded.Soul);
        Assert.Equal(row, Assert.Single(decoded.Guardians));
        Assert.Equal(row, SoulMessages.DecodeGuardian(SoulMessages.EncodeGuardian(row)));
        Assert.Equal(42u, SoulMessages.DecodeSoul(SoulMessages.EncodeSoul(42)));
    }
}
