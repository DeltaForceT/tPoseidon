using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.Game.Tests.Characters;

public sealed class DisguiseTests
{
    [Fact]
    public void AKitDisguisesANormalFormPlayerAndRemovalRestoresTheRealType()
    {
        var state = new DisguiseState();
        state.Reset(3);
        Assert.Equal(DisguiseUse.Disguised, state.UseKit(2075, 3, normalForm: true));
        Assert.True(state.Disguised);
        Assert.Equal(2075, state.ShownType);
        Assert.True(state.Remove(3));
        Assert.Equal((3, false, false), (state.ShownType, state.LooksDifferent, state.Disguised));
        Assert.False(state.Remove(3));
    }

    [Fact]
    public void MissingRowSameTypeAndOtherFormsAreRefused()
    {
        var state = new DisguiseState();
        state.Reset(3);
        Assert.Equal(DisguiseUse.NoKitRow, state.UseKit(null, 3, normalForm: true));
        Assert.Equal(DisguiseUse.Refused, state.UseKit(3, 3, normalForm: true));
        Assert.Equal(DisguiseUse.Refused, state.UseKit(2075, 3, normalForm: false));
        Assert.False(state.Disguised);
    }

    [Fact]
    public void ASecondKitWhileDisguisedIsRefusedButKeepsTheNewShownType()
    {
        var state = new DisguiseState();
        state.Reset(3);
        state.UseKit(2075, 3, normalForm: true);
        Assert.Equal(DisguiseUse.Refused, state.UseKit(2020, 3, normalForm: false));
        Assert.Equal(2020, state.ShownType);
        Assert.True(state.Disguised);
    }

    [Fact]
    public void ShownTypesOutsideTheRangeAreNotStored()
    {
        var state = new DisguiseState();
        state.Reset(3);
        Assert.Equal(DisguiseUse.Refused, state.UseKit(0x4672, 3, normalForm: true));
        Assert.Equal(3, state.ShownType);
    }

    [Fact]
    public void DigRefusalCodes()
    {
        Assert.Equal((byte)0x10, DisguiseRules.DigRefusal(disguised: true, otherForm: false));
        Assert.Equal((byte)0x16, DisguiseRules.DigRefusal(disguised: false, otherForm: true));
        Assert.Null(DisguiseRules.DigRefusal(disguised: false, otherForm: false));
    }

    [Fact]
    public void WeddingDressTypeAddsTheCharacterTypeAndOddTypesAreFemale()
    {
        Assert.Equal(18009, DisguiseRules.DressType(1, 1));
        Assert.Equal(18008 + 8, DisguiseRules.DressType(1, 8));
        Assert.Equal(18008 + 2, DisguiseRules.DressType(0, 2));
        Assert.True(DisguiseRules.IsFemale(1));
        Assert.False(DisguiseRules.IsFemale(2));
        Assert.False(DisguiseRules.Storable(DisguiseRules.DressType(4, 2)));
    }
}
