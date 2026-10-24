using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class EquipmentRulesTests
{
    private const int Equipable = EquipmentRules.EquipableOption;
    private static readonly int[] NoStats = new int[EquipmentRules.ItemStatCount];

    private static ItemParamEntry Definition(int id, int type, int subType = 0, int options = Equipable, int minLevel = 0) =>
        new() { Id = id, Class = 2, Type = type, SubType = subType, Options = options, MinLevel = minLevel };

    private static Item Item(ulong uid, int itemId, byte slotKind = SlotKind.Inventory, byte position = 0, byte refineState = 0) =>
        new()
        {
            ItemUid = uid,
            ItemIndex = itemId,
            ItemClass = 2,
            CharacterId = 7,
            SlotKind = slotKind,
            SlotPosition = position,
            RefineState = refineState,
        };

    private static WornItem Worn(byte slot, ulong uid, ItemParamEntry definition) =>
        new(slot, Item(uid, definition.Id, SlotKind.Equipment, slot), definition);

    [Theory]
    [InlineData(3, 0)]
    [InlineData(2, 1)]
    [InlineData(8, 2)]
    [InlineData(4, 3)]
    [InlineData(7, 4)]
    [InlineData(17, 5)]
    [InlineData(18, 6)]
    [InlineData(6, 7)]
    [InlineData(13, 8)]
    [InlineData(19, 10)]
    [InlineData(26, 17)]
    [InlineData(5, 18)]
    [InlineData(27, 19)]
    [InlineData(34, 21)]
    [InlineData(15, EquipmentRules.NoSlot)]
    [InlineData(29, EquipmentRules.NoSlot)]
    public void EachItemTypeHasItsSlot(int type, int slot)
    {
        Assert.Equal(slot, EquipmentRules.SlotOfType(type));
    }

    [Fact]
    public void AccessoriesFillTheFirstAccessorySlotThenTheSecond()
    {
        var accessory = Definition(5000, EquipmentRules.AccessoryType, subType: 7);

        Assert.Equal(EquipmentSlot.Accessory, EquipmentRules.SlotFor(accessory, _ => false));
        Assert.Equal(EquipmentSlot.SecondAccessory, EquipmentRules.SlotFor(accessory, slot => slot == EquipmentSlot.Accessory));
        Assert.Equal(EquipmentSlot.Accessory, EquipmentRules.SlotFor(accessory, slot => slot is EquipmentSlot.Accessory or EquipmentSlot.SecondAccessory));
    }

    [Fact]
    public void SpeedAccessoriesTakeTheirOwnSlotWhenItIsFree()
    {
        var charm = Definition(5001, EquipmentRules.AccessoryType, subType: EquipmentRules.SpeedAccessorySubType);

        Assert.Equal(EquipmentSlot.SpeedAccessory, EquipmentRules.SlotFor(charm, _ => false));
        Assert.Equal(EquipmentSlot.Accessory, EquipmentRules.SlotFor(charm, slot => slot == EquipmentSlot.SpeedAccessory));
    }

    [Fact]
    public void WithoutGroupOrGenderTheMaskIsTheTypeFlagsColumn()
    {
        var definition = Definition(1, 3, options: Equipable | EquipmentRules.CharacterTypeOption) with { CharacterTypeFlags = 0x2 | 0x400 };

        Assert.Equal(0x402u, EquipmentRules.CharacterTypeMask(definition));
        Assert.True(EquipmentRules.FitsCharacterType(0x402u, 2));
        Assert.True(EquipmentRules.FitsCharacterType(0x402u, 11));
        Assert.False(EquipmentRules.FitsCharacterType(0x402u, 1));
    }

    [Theory]
    [InlineData(1, 0x040E_0703u)]
    [InlineData(2, 0x0030_180Cu)]
    [InlineData(4, 0x00C0_6030u)]
    [InlineData(8, 0x0301_80C0u)]
    [InlineData(3, 0x043E_1F0Fu)]
    public void GroupFlagsKeepOnlyTheirTypes(int groupFlags, uint mask)
    {
        var definition = Definition(1, 3) with { CharacterGroupFlags = groupFlags };

        Assert.Equal(mask, EquipmentRules.CharacterTypeMask(definition));
        Assert.True(EquipmentRules.RestrictsCharacterType(definition));
    }

    [Theory]
    [InlineData(1, 0x02A9_54AAu)]
    [InlineData(2, 0x0556_AB55u)]
    public void GenderKeepsHalfOfTheTypes(int gender, uint mask)
    {
        var definition = Definition(1, 3) with { CharacterGender = gender };

        Assert.Equal(mask, EquipmentRules.CharacterTypeMask(definition));
    }

    [Fact]
    public void AnItemWithoutTheEquipableOptionIsRefused()
    {
        var hat = Definition(4030, 3, options: 0);

        Assert.Equal(EquipRefusal.NotEquipable, Check(hat, Item(1, 4030), EquipmentSlot.Hat));
    }

    [Fact]
    public void ABrokenItemNeedsTheBrokenEquipableOption()
    {
        var hat = Definition(4030, 3);
        var mendedHat = Definition(4031, 3, options: Equipable | EquipmentRules.BrokenEquipableOption);

        Assert.Equal(EquipRefusal.NotEquipable, Check(hat, Item(1, 4030, refineState: EquipmentRules.BrokenRefineState), EquipmentSlot.Hat));
        Assert.Null(Check(mendedHat, Item(2, 4031, refineState: EquipmentRules.BrokenRefineState), EquipmentSlot.Hat));
    }

    [Fact]
    public void GmItemsAreRefused()
    {
        var drill = Definition(8017, 6) with { ExtendedOptions = EquipmentRules.GmOnlyExtendedOption };

        Assert.Equal(EquipRefusal.GmOnly, Check(drill, Item(1, 8017), EquipmentSlot.Drill));
    }

    [Fact]
    public void TheCharacterTypeMustBeInTheMask()
    {
        var vest = Definition(14001, 19, options: Equipable | EquipmentRules.CharacterTypeOption) with { CharacterTypeFlags = 0x2 };

        Assert.Equal(EquipRefusal.WrongCharacterType, Check(vest, Item(1, 14001), EquipmentSlot.FirstCostume, characterType: 1));
        Assert.Null(Check(vest, Item(1, 14001), EquipmentSlot.FirstCostume, characterType: 2));
    }

    [Fact]
    public void TheLevelMustReachMinLevel()
    {
        var sword = Definition(3031, 2, minLevel: 10);

        Assert.Equal(EquipRefusal.LevelTooLow, Check(sword, Item(1, 3031), EquipmentSlot.Weapon, level: 9));
        Assert.Null(Check(sword, Item(1, 3031), EquipmentSlot.Weapon, level: 10));
    }

    [Fact]
    public void TheStatRequirementUsesTheStatTotals()
    {
        var staff = Definition(3100, 2) with { MinStatType = 4, MinStatLevel = 60 };
        var totals = new[] { 0, 0, 0, 0, 59, 0, 0, 0, 0, 0, 0, 0 };

        Assert.Equal(EquipRefusal.StatTooLow, EquipmentRules.CheckEquip(staff, Item(1, 3100), EquipmentSlot.Weapon, Array.Empty<WornItem>(), 50, 1, totals, _ => true));
        totals[4] = 60;
        Assert.Null(EquipmentRules.CheckEquip(staff, Item(1, 3100), EquipmentSlot.Weapon, Array.Empty<WornItem>(), 50, 1, totals, _ => true));
    }

    [Fact]
    public void TheSlotMustMatchTheItemType()
    {
        var hat = Definition(4030, 3);

        Assert.Equal(EquipRefusal.WrongSlot, Check(hat, Item(1, 4030), EquipmentSlot.Weapon));
    }

    [Fact]
    public void AnAccessoryMayGoToAnyAccessorySlot()
    {
        var ring = Definition(5000, EquipmentRules.AccessoryType, subType: 7);

        Assert.Null(Check(ring, Item(1, 5000), EquipmentSlot.SecondAccessory));
        Assert.Null(Check(ring, Item(1, 5000), EquipmentSlot.SpeedAccessory));
        Assert.Equal(EquipRefusal.WrongSlot, Check(ring, Item(1, 5000), EquipmentSlot.Belt));
    }

    [Fact]
    public void AWornItemCannotBeEquippedAgain()
    {
        var hat = Definition(4030, 3);
        var worn = new[] { Worn(EquipmentSlot.Hat, 1, hat) };

        Assert.Equal(EquipRefusal.AlreadyEquipped, Check(hat, Item(1, 4030, SlotKind.Equipment), EquipmentSlot.Hat, worn));
    }

    [Fact]
    public void TheSameAccessoryCannotBeWornTwice()
    {
        var ring = Definition(5000, EquipmentRules.AccessoryType, subType: 7);
        var worn = new[] { Worn(EquipmentSlot.Accessory, 1, ring) };

        Assert.Equal(EquipRefusal.SameAccessory, Check(ring, Item(2, 5000), EquipmentSlot.SecondAccessory, worn));
    }

    [Fact]
    public void OneAccessoryOfSubType52IsAllowed()
    {
        var amulet = Definition(5100, EquipmentRules.AccessoryType, subType: 52);
        var otherAmulet = Definition(5101, EquipmentRules.AccessoryType, subType: 52);
        var worn = new[] { Worn(EquipmentSlot.Accessory, 1, amulet) };

        Assert.Equal(EquipRefusal.SameAccessoryKind, Check(otherAmulet, Item(2, 5101), EquipmentSlot.SecondAccessory, worn));
    }

    [Fact]
    public void AShieldIsRefusedWhileAGunIsWorn()
    {
        var gun = Definition(3500, 2, subType: EquipmentRules.GunSubType);
        var shield = Definition(6530, EquipmentRules.ShieldType);
        var worn = new[] { Worn(EquipmentSlot.Weapon, 1, gun) };

        Assert.Equal(EquipRefusal.ShieldWithGun, Check(shield, Item(2, 6530), EquipmentSlot.Shield, worn));
    }

    [Fact]
    public void EquippingOverAnItemReleasesIt()
    {
        var oldHat = Definition(4030, 3);
        var newHat = Definition(4031, 3);
        var worn = new[] { Worn(EquipmentSlot.Hat, 1, oldHat) };

        var releases = EquipmentRules.ReleasesBeforeEquip(newHat, EquipmentSlot.Hat, worn);

        Assert.NotNull(releases);
        Assert.Equal(1UL, Assert.Single(releases!).Item.ItemUid);
    }

    [Fact]
    public void AGunAlsoReleasesTheShield()
    {
        var sword = Definition(3030, 2);
        var shield = Definition(6530, EquipmentRules.ShieldType);
        var gun = Definition(3500, 2, subType: EquipmentRules.GunSubType);
        var worn = new[] { Worn(EquipmentSlot.Weapon, 1, sword), Worn(EquipmentSlot.Shield, 2, shield) };

        var releases = EquipmentRules.ReleasesBeforeEquip(gun, EquipmentSlot.Weapon, worn);

        Assert.Equal(new ulong[] { 1, 2 }, releases!.Select(release => release.Item.ItemUid));
    }

    [Fact]
    public void TheSecondAccessorySlotCannotBeSwappedWhileTheFirstIsTaken()
    {
        var ring = Definition(5000, EquipmentRules.AccessoryType, subType: 7);
        var bracer = Definition(5002, EquipmentRules.AccessoryType, subType: 7);
        var lens = Definition(5003, EquipmentRules.AccessoryType, subType: 7);
        var worn = new[] { Worn(EquipmentSlot.Accessory, 1, ring), Worn(EquipmentSlot.SecondAccessory, 2, bracer) };

        Assert.Null(EquipmentRules.ReleasesBeforeEquip(lens, EquipmentSlot.SecondAccessory, worn));
        Assert.NotNull(EquipmentRules.ReleasesBeforeEquip(lens, EquipmentSlot.Accessory, worn));
    }

    [Fact]
    public void EarsAndTailStayOnBelowLevel20()
    {
        var ears = Definition(6901, EquipmentRules.EarsType);
        var worn = Worn(EquipmentSlot.Ear, 1, ears);

        Assert.Equal(ReleaseRefusal.EarsOrTailBelowLevel, EquipmentRules.CheckRelease(worn, EquipmentSlot.Ear, new[] { worn }, 19));
        Assert.Null(EquipmentRules.CheckRelease(worn, EquipmentSlot.Ear, new[] { worn }, 20));
    }

    [Fact]
    public void AReleaseNamingAnotherItemsSlotIsRefused()
    {
        var hat = Definition(4030, 3);
        var sword = Definition(3030, 2);
        var wornHat = Worn(EquipmentSlot.Hat, 1, hat);
        var wornSword = Worn(EquipmentSlot.Weapon, 2, sword);

        Assert.Equal(ReleaseRefusal.SlotHoldsAnotherItem, EquipmentRules.CheckRelease(wornHat, EquipmentSlot.Weapon, new[] { wornHat, wornSword }, 50));
        Assert.Null(EquipmentRules.CheckRelease(wornHat, EquipmentSlot.Belt, new[] { wornHat, wornSword }, 50));
    }

    [Fact]
    public void WornItemsAddTheirStatColumns()
    {
        var sword = Definition(3030, 2) with { StatBonuses = new[] { 12, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 } };
        var hat = Definition(4030, 3) with { StatBonuses = new[] { 0, 5, 0, 0, 0, 0, 100, 0, 0, 30, 0, 0 } };
        var stats = new EffectiveStats(10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120);

        var geared = EquipmentRules.WithEquipment(stats, new[] { sword, hat });

        Assert.Equal(new EffectiveStats(22, 25, 30, 40, 50, 60, 170, 80, 90, 130, 110, 120), geared);
    }

    private static ItemParamEntry Common(int id, int type, int subType = 0) =>
        new() { Id = id, Class = EquipmentRules.CommonClass, Type = type, SubType = subType, Options = Equipable };

    [Fact]
    public void BulletsNeedAWornGunAndADifferentLoadedBullet()
    {
        var gun = Definition(610017, 2, EquipmentRules.GunSubType);
        var bullet = Common(5500, EquipmentRules.BulletType);
        var otherBullet = Common(5501, EquipmentRules.BulletType);
        var wornGun = Worn(EquipmentSlot.Weapon, 1, gun);

        Assert.Equal(EquipRefusal.BulletWithoutGun, EquipmentRules.CheckEquipCommon(bullet, Item(2, 5500), Array.Empty<WornItem>(), 50, 1, NoStats));
        Assert.Null(EquipmentRules.CheckEquipCommon(bullet, Item(2, 5500), new[] { wornGun }, 50, 1, NoStats));
        Assert.Equal(
            EquipRefusal.SameBulletLoaded,
            EquipmentRules.CheckEquipCommon(bullet, Item(2, 5500), new[] { wornGun, Worn(EquipmentSlot.Common, 3, bullet) }, 50, 1, NoStats));
        Assert.Null(EquipmentRules.CheckEquipCommon(bullet, Item(2, 5500), new[] { wornGun, Worn(EquipmentSlot.Common, 3, otherBullet) }, 50, 1, NoStats));
    }

    [Fact]
    public void OnlyBulletsThrowItemsAndThrowCardsGoInTheCommonSlot()
    {
        Assert.Equal(EquipRefusal.NotCommonItem, EquipmentRules.CheckEquipCommon(Common(4030, 3), Item(1, 4030), Array.Empty<WornItem>(), 50, 1, NoStats));
        Assert.Null(EquipmentRules.ReleasesBeforeEquipCommon(Common(22002, EquipmentRules.CardType, 18), Array.Empty<WornItem>()));
        Assert.True(EquipmentRules.IsCommonSlotItem(Common(1, EquipmentRules.CardType, EquipmentRules.ThrowCardSubType)));
        Assert.False(EquipmentRules.IsCommonSlotItem(Common(1, EquipmentRules.CardType, 18)));
    }

    [Fact]
    public void LoadingABulletReleasesTheLoadedItemAndAThrowItemReleasesTheGun()
    {
        var gun = Worn(EquipmentSlot.Weapon, 1, Definition(610017, 2, EquipmentRules.GunSubType));
        var loaded = Worn(EquipmentSlot.Common, 3, Common(5501, EquipmentRules.BulletType));

        Assert.Equal(new[] { loaded }, EquipmentRules.ReleasesBeforeEquipCommon(Common(5500, EquipmentRules.BulletType), new[] { gun, loaded }));
        Assert.Equal(new[] { gun, loaded }, EquipmentRules.ReleasesBeforeEquipCommon(Common(9000, EquipmentRules.ThrowItemType), new[] { gun, loaded }));
    }

    [Fact]
    public void AGunReleasesALoadedThrowItemButKeepsBullets()
    {
        var gun = Definition(610017, 2, EquipmentRules.GunSubType);
        var thrown = Worn(EquipmentSlot.Common, 3, Common(9000, EquipmentRules.ThrowItemType));
        var bullet = Worn(EquipmentSlot.Common, 3, Common(5500, EquipmentRules.BulletType));

        Assert.Equal(new[] { thrown }, EquipmentRules.ReleasesBeforeEquip(gun, EquipmentSlot.Weapon, new[] { thrown }));
        Assert.Empty(EquipmentRules.ReleasesBeforeEquip(gun, EquipmentSlot.Weapon, new[] { bullet })!);
    }

    [Fact]
    public void OnlyTheLoadedCommonItemCanBeUnloadedFromSlotTwenty()
    {
        var bullet = Common(5500, EquipmentRules.BulletType);
        var loaded = Worn(EquipmentSlot.Common, 3, bullet);

        Assert.Null(EquipmentRules.CheckReleaseCommon(bullet, loaded.Item, EquipmentSlot.Common, new[] { loaded }));
        Assert.Equal(ReleaseRefusal.NotLoaded, EquipmentRules.CheckReleaseCommon(bullet, Item(4, 5500), EquipmentSlot.Common, new[] { loaded }));
        Assert.Equal(ReleaseRefusal.NotLoaded, EquipmentRules.CheckReleaseCommon(bullet, loaded.Item, EquipmentSlot.Weapon, new[] { loaded }));
    }

    [Fact]
    public void AGunNeedsTheGunCarrierAbility()
    {
        var gun = Definition(3500, EquipmentRules.WeaponType, subType: EquipmentRules.GunSubType);

        Assert.Equal(EquipRefusal.MissingWeaponAbility, Check(gun, Item(1, 3500), EquipmentSlot.Weapon, holdsAbility: _ => false));
        Assert.Equal(EquipRefusal.MissingWeaponAbility, Check(gun, Item(1, 3500), EquipmentSlot.Weapon, holdsAbility: ability => ability != EquipmentRules.GunCarrierAbility));
        Assert.Null(Check(gun, Item(1, 3500), EquipmentSlot.Weapon, holdsAbility: ability => ability == EquipmentRules.GunCarrierAbility));
    }

    [Fact]
    public void AHammerNeedsTheHammerAbility()
    {
        var hammer = Definition(4700, EquipmentRules.WeaponType, subType: EquipmentRules.HammerSubType);

        Assert.Equal(EquipRefusal.MissingWeaponAbility, Check(hammer, Item(1, 4700), EquipmentSlot.Weapon, holdsAbility: _ => false));
        Assert.Equal(EquipRefusal.MissingWeaponAbility, Check(hammer, Item(1, 4700), EquipmentSlot.Weapon, holdsAbility: ability => ability == EquipmentRules.GunCarrierAbility));
        Assert.Null(Check(hammer, Item(1, 4700), EquipmentSlot.Weapon, holdsAbility: ability => ability == EquipmentRules.HammerAbility));
    }

    [Theory]
    [InlineData(EquipmentRules.WeaponType, 2)]
    [InlineData(EquipmentRules.WeaponType, 0)]
    [InlineData(EquipmentRules.WeaponType, 61)]
    [InlineData(EquipmentRules.AccessoryType, EquipmentRules.HammerSubType)]
    [InlineData(EquipmentRules.ShieldType, 0)]
    [InlineData(3, 0)]
    public void EveryOtherItemIsNotAskedForAnAbility(int type, int subType)
    {
        var definition = Definition(900, type, subType);
        var slot = (byte)(type == EquipmentRules.AccessoryType ? EquipmentSlot.Accessory : EquipmentRules.SlotOfType(type));

        Assert.Null(EquipmentRules.RequiredAbility(definition));
        Assert.Null(Check(definition, Item(1, 900), slot, holdsAbility: _ => false));
    }

    [Fact]
    public void TheAbilityCheckComesAfterTheSlotCheckAndBeforeTheShieldRule()
    {
        var gun = Definition(3500, EquipmentRules.WeaponType, subType: EquipmentRules.GunSubType);
        var shield = Definition(6530, EquipmentRules.ShieldType);
        var worn = new[] { Worn(EquipmentSlot.Weapon, 1, gun) };

        Assert.Equal(EquipRefusal.WrongSlot, Check(gun, Item(2, 3500), EquipmentSlot.Shield, holdsAbility: _ => false));
        Assert.Equal(EquipRefusal.ShieldWithGun, Check(shield, Item(3, 6530), EquipmentSlot.Shield, worn, holdsAbility: _ => false));
    }

    private static EquipRefusal? Check(
        ItemParamEntry definition,
        Item item,
        byte slot,
        IReadOnlyList<WornItem>? worn = null,
        int level = 50,
        int characterType = 1,
        Func<int, bool>? holdsAbility = null) =>
        EquipmentRules.CheckEquip(definition, item, slot, worn ?? Array.Empty<WornItem>(), level, characterType, NoStats, holdsAbility ?? (_ => true));
}
