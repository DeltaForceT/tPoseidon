using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ItemComposingRulesTests
{
    private const int Composable = 0x1 | 0x10 | 0x1000;
    private static readonly ItemProperty[] NoProperties = [];

    private static ItemParamEntry Equipment(int id = 87003, int type = 2, int subType = 2, int options = Composable) =>
        new() { Id = id, Class = 2, Type = type, SubType = subType, Options = options };

    private static readonly ItemParamEntry Flame = new() { Id = 19689, Class = 1, Type = 14, Effect = 36 };
    private static readonly ItemParamEntry PetBook = new() { Id = 19748, Class = 1, Type = 14, Effect = 40 };

    private static byte Check(ItemParamEntry? baseItem, ItemParamEntry? material, bool supportGiven, ItemParamEntry? support,
        IReadOnlyList<ItemProperty>? baseProperties = null) =>
        ItemComposingRules.Check(new ComposingCandidate(baseItem, baseProperties ?? NoProperties, material, NoProperties, supportGiven, support));

    [Fact]
    public void BagChecksComeFirst()
    {
        Assert.Equal(ItemComposingRules.NotInBag, Check(null, Equipment(), true, Flame));
        Assert.Equal(ItemComposingRules.NotInBag, Check(Equipment(), null, true, Flame));
        Assert.Equal(ItemComposingRules.NotInBag, Check(Equipment(), Equipment(), true, null));
        Assert.Equal(ItemComposingRules.Ok, Check(Equipment(options: Composable | 0x4000000), Equipment(), true, null));
    }

    [Fact]
    public void OnlyFlaggedWeaponsHatsShieldsHeadsFacesAndCapesCompose()
    {
        foreach (var type in new[] { 2, 3, 8, 0x18, 0x19, 0x22 })
        {
            Assert.Equal(ItemComposingRules.Ok, Check(Equipment(type: type), Equipment(type: type), true, Flame));
        }
        Assert.Equal(ItemComposingRules.NotComposable, Check(Equipment(type: 7), Equipment(type: 7), true, Flame));
        Assert.Equal(ItemComposingRules.NotComposable, Check(Equipment(options: 0x10), Equipment(), true, Flame));
        foreach (var blocking in new[] { 0x100000, 0x200000, 0x800000 })
        {
            Assert.Equal(ItemComposingRules.NotComposable, Check(Equipment(), Equipment(options: Composable | blocking), true, Flame));
        }
    }

    [Fact]
    public void KindAndSubTypeMustMatchUnlessOneIsTheAnySubType()
    {
        Assert.Equal(ItemComposingRules.DifferentKind, Check(Equipment(type: 2), Equipment(type: 3), true, Flame));
        Assert.Equal(ItemComposingRules.DifferentKind, Check(Equipment(subType: 2), Equipment(subType: 54), true, Flame));
        Assert.Equal(ItemComposingRules.Ok, Check(Equipment(subType: 0x4E), Equipment(subType: 54), true, Flame));
        Assert.Equal(ItemComposingRules.Ok, Check(Equipment(subType: 54), Equipment(subType: 0x4E), true, Flame));
    }

    [Fact]
    public void CatalystIsTheFlameUnlessTheBaseNeedsNone()
    {
        Assert.Equal(ItemComposingRules.CatalystMissing, Check(Equipment(), Equipment(), false, null));
        Assert.Equal(ItemComposingRules.BadCatalyst, Check(Equipment(), Equipment(), true, PetBook));
        Assert.Equal(ItemComposingRules.Ok, Check(Equipment(options: Composable | 0x4000000), Equipment(), false, null));
        Assert.False(ItemComposingRules.NeedsCatalyst(Equipment(options: Composable | 0x4000000), NoProperties));
    }

    [Fact]
    public void TheOptionsPropertyOverridesTheTableWord()
    {
        var overridden = new[] { new ItemProperty(13, 26001, 0, 0x10) };
        Assert.Equal(0x10, ItemComposingRules.Options(Equipment(), overridden));
        Assert.Equal(Composable, ItemComposingRules.Options(Equipment(), NoProperties));
        Assert.Equal(ItemComposingRules.NotComposable, Check(Equipment(), Equipment(), true, Flame, overridden));
    }

    [Fact]
    public void LookComesFromTheMaterialOrTheLookItCarries()
    {
        var fresh = ItemComposingRules.Look(Equipment(), NoProperties, 87003, NoProperties);
        Assert.Equal(new ComposedLook(87003, Composable | 0x1000, true), fresh);
        var carried = ItemComposingRules.Look(Equipment(), [new ItemProperty(12, 26000, 0, 500014)], 87003, [new ItemProperty(12, 26000, 0, 88002)]);
        Assert.Equal(new ComposedLook(88002, Composable | 0x1000, false), carried);
    }

    [Fact]
    public void ApplyReplacesOnlyTheLookAndOptionsProperties()
    {
        var stored = new[] { new ItemProperty(10, 10000, 120, 0), new ItemProperty(12, 26000, 0, 1), new ItemProperty(13, 26001, 0, 2) };
        var look = new ComposedLook(87003, 0x1011, false);
        var applied = ItemComposingRules.Apply(stored, look);
        Assert.Equal(new[] { new ItemProperty(10, 10000, 120, 0), new ItemProperty(12, 26000, 0, 87003), new ItemProperty(13, 26001, 0, 0x1011) }, applied);
    }

    [Fact]
    public void PetsComposeWithTheBookAndADiscipleAtLeastAsHighAsTheTeacher()
    {
        ItemParamEntry Pet(int level) => new() { Id = 90000 + level, Class = 2, Type = ItemComposingRules.PetType, SubType = 1, Options = Composable, MinLevel = level };
        byte PetCheck(ItemParamEntry teacher, ItemParamEntry disciple, ItemParamEntry? support) =>
            ItemComposingRules.Check(new ComposingCandidate(teacher, NoProperties, disciple, NoProperties, support is not null, support, ItemComposingRules.PetKind));
        Assert.Equal(ItemComposingRules.Ok, PetCheck(Pet(30), Pet(30), PetBook));
        Assert.Equal(ItemComposingRules.PetLevelLow, PetCheck(Pet(30), Pet(20), PetBook));
        Assert.Equal(ItemComposingRules.BadCatalyst, PetCheck(Pet(30), Pet(40), Flame));
        Assert.Equal(ItemComposingRules.NotComposable, PetCheck(Equipment(), Equipment(), PetBook));
        var presents = new[] { new PetComposingPresent(62322, 1, 400, true), new PetComposingPresent(62322, 1, 400, false) };
        Assert.Equal(62322, ItemComposingRules.PetPresent(presents, 30, false));
        Assert.Null(ItemComposingRules.PetPresent(presents, 400, false));
    }

    [Fact]
    public void DecomposingNeedsALookTheFlameAndAtMostTwentyProperties()
    {
        var look = new ItemProperty(ItemComposingRules.AppearanceType, ItemComposingRules.AppearanceId, 0, 87010);
        var stat = new ItemProperty(ItemPropertyTypes.Stat, 10000, 5, 0);
        Assert.Equal(ItemComposingRules.Ok, DecomposeRules.Check(new[] { look, stat }, Flame));
        Assert.Equal(DecomposeRules.NotInBag, DecomposeRules.Check(new[] { look }, null));
        Assert.Equal(DecomposeRules.BadCatalyst, DecomposeRules.Check(new[] { look }, PetBook));
        Assert.Equal(DecomposeRules.NotDecomposable, DecomposeRules.Check(new[] { stat }, Flame));
        Assert.Equal(DecomposeRules.NotDecomposable, DecomposeRules.Check(Enumerable.Repeat(look, 21).ToArray(), Flame));
        Assert.Equal(1, DecomposeRules.DummyRow(ItemComposingRules.WeaponType, DecomposeRules.GunSubType));
        Assert.Equal(7, DecomposeRules.DummyRow(ItemComposingRules.WeaponType, DecomposeRules.HammerSubType));
        Assert.Equal(0, DecomposeRules.DummyRow(ItemComposingRules.WeaponType, 2));
        Assert.Equal(6, DecomposeRules.DummyRow(ItemComposingRules.CapeType, 0));
        Assert.Null(DecomposeRules.DummyRow(7, 0));
    }
}
