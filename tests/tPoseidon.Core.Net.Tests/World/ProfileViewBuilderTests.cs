using System.Text;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class ProfileViewBuilderTests
{
    private static ProfileSource Source(uint flags, ProfileGear? gear = null, string message = "") => new(
        flags, "Kim", 0x0102, 2, 5, 40, 7, 5, "Guild", new ProfileTitle(1, 9), 70000, 3, message, [4, 2, 1, 3],
        gear ?? ProfileGear.Empty);

    private static ProfileGear Gear() => new(
        [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200],
        [90, 190, 290, 390, 490, 590, 690, 790, 890, 990, 1090, 1290],
        [0, 3, 4, 7, 8, 399, 0, 0, 0, 0, 0, 0],
        Enumerable.Range(0, 22).Select(slot => slot * 10).ToArray(),
        []);

    [Fact]
    public void NothingIsSharedWithoutTheInfoFlag()
    {
        var view = ProfileViewBuilder.Build(Source(0, Gear()), Encoding.ASCII);
        Assert.Null(view.Info);
        Assert.Null(view.Equipment);
        Assert.Equal("Kim", view.Name);
        Assert.Equal((byte)2, view.FType);
    }

    [Fact]
    public void TheEquipmentFlagNeedsTheInfoFlag()
    {
        var view = ProfileViewBuilder.Build(Source(ProfileViewRules.ShowEquipment, Gear()), Encoding.ASCII);
        Assert.Null(view.Info);
        Assert.Null(view.Equipment);
    }

    [Fact]
    public void TheInfoCarriesTheCounterWordsAndTheEncodedMessage()
    {
        var view = ProfileViewBuilder.Build(Source(ProfileViewRules.DefaultFlags & ProfileViewRules.ShowInfo, null, "hi"), Encoding.ASCII);
        var info = Assert.IsType<ProfileViewInfo>(view.Info);
        Assert.Equal((ushort)(70000 - 65536), info.Kills);
        Assert.Equal((ushort)3, info.Deaths);
        Assert.Equal(new byte[] { (byte)'h', (byte)'i' }, info.Message);
        Assert.Equal("Guild", info.GuildName);
        Assert.Equal(new short[] { 4, 2, 1, 3 }, info.GrowthWeights);
        Assert.Null(view.Equipment);
    }

    [Fact]
    public void AMessageOverTwoHundredBytesIsSentEmpty()
    {
        var view = ProfileViewBuilder.Build(Source(1, null, new string('a', 201)), Encoding.ASCII);
        Assert.Empty(view.Info!.Message);
        view = ProfileViewBuilder.Build(Source(1, null, new string('a', 200)), Encoding.ASCII);
        Assert.Equal(200, view.Info!.Message.Length);
    }

    [Fact]
    public void TheEquipmentBlockSplitsTotalsIntoTotalAndBonusAndPointsIntoLevelAndRemainder()
    {
        var view = ProfileViewBuilder.Build(Source(3, Gear()), Encoding.ASCII);
        var equipment = Assert.IsType<ProfileViewEquipment>(view.Equipment);
        Assert.Equal(new uint[] { 100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1200 }, equipment.Totals);
        Assert.Equal(
            new uint[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, unchecked((uint)-90) },
            equipment.Bonuses);
        Assert.Equal(new ushort[] { 1, 1, 2, 2, 3, 100, 1, 1, 1, 1, 1, 1 }, equipment.StatLevels);
        Assert.Equal(new byte[] { 0, 3, 0, 3, 0, 3, 0, 0, 0, 0, 0, 0 }, equipment.Remainders);
        Assert.Equal(22, equipment.Resists.Count);
        Assert.Equal((short)210, equipment.Resists[21]);
    }

    [Fact]
    public void ABonusIsTheTotalMinusTheTableAndAFallenStatWrapsAsADword()
    {
        var gear = Gear() with { Table = [100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 1100, 1300] };
        var equipment = ProfileViewBuilder.Build(Source(3, gear), Encoding.ASCII).Equipment!;
        Assert.Equal(0u, equipment.Bonuses[0]);
        Assert.Equal(unchecked((uint)-100), equipment.Bonuses[11]);
    }

    [Fact]
    public void ATotalPastASignedDwordIsClamped()
    {
        var gear = Gear() with { Totals = [long.MaxValue, -5, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0] };
        var equipment = ProfileViewBuilder.Build(Source(3, gear), Encoding.ASCII).Equipment!;
        Assert.Equal((uint)int.MaxValue, equipment.Totals[0]);
        Assert.Equal(0u, equipment.Totals[1]);
    }
}
