using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Items;

public static class SpecialItemEffects
{
    public const int PortablePort = 14;
    public const int HairDye = 15;
    public const int EmergencyPort = 19;
    public const int PortableAdvancedPort = 20;
    public const int GenderChange = 21;
    public const int SkillReset = 23;
    public const int StatReset = 24;
    public const int GrowthGraphReset = 25;
    public const int StatResetAll = 26;
    public const int StorageExpansion = 27;
    public const int BroomPort = 28;
    public const int Rename = 31;
    public const int Megaphone = 32;
    public const int Microphone = 33;
    public const int Documents = 35;
    public const int FriendFinder = 39;

    public const int CustomDataOption = 0x8000;
    public const int NormalFormRefusalKind = 8;

    public static bool NeedsCustomData(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return (definition.Options & CustomDataOption) != 0;
    }

    public static bool RequiresNormalForm(int effect) =>
        effect is HairDye or GenderChange or SkillReset or StatReset or GrowthGraphReset or StatResetAll;

    public static bool UsesCustomData(int effect) =>
        effect is PortablePort or HairDye or PortableAdvancedPort or GenderChange or SkillReset or StatReset or GrowthGraphReset
            or Rename or Megaphone or FriendFinder;

    public static bool Handles(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return IsBuilt(definition.Effect) || IsPlainUse(definition.Effect, definition.SubType);
    }

    public static bool IsBuilt(int effect) =>
        effect is PortablePort or HairDye or EmergencyPort or PortableAdvancedPort or GenderChange or SkillReset or StatReset
            or GrowthGraphReset or StatResetAll or StorageExpansion or BroomPort or Rename or Megaphone or FriendFinder;

    public static bool IsPlainUse(int effect, int subType) =>
        (effect is 0 or Microphone or Documents) && subType is not (69 or 71 or 72 or 74 or 75 or 77);
}

public static class PortRules
{
    public const int FirstZone = 1;
    public const int LastZone = 999;
    public const int LastPortableZone = 7;
    public const int MapRefusesWarpOption = 0x80;
    public const int MapRefusesArrivalOption = 0x400;
    public const uint MapRefusalCode = 4;
    public const int PortableDataBytes = 2;
    public const int AdvancedDataBytes = 4;
    public const int FriendFinderDataBytes = 6;
    public const byte ChangeZoneType = TeleportArrival.ChangeZoneType;
    public static readonly TimeSpan WarpDelay = MemoryPortRules.WarpDelay;

    public static bool IsZone(int zone) => zone is >= FirstZone and <= LastZone;

    public static bool MapRefusesWarp(int mapOption) => (mapOption & MapRefusesWarpOption) != 0;

    public static bool MapRefusesArrival(int mapOption) => (mapOption & MapRefusesArrivalOption) != 0;

    public static int? PickRandomZone(RandomPortEntry entry, Func<int, int> nextBelow)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(nextBelow);
        var total = entry.Zones.Sum(zone => zone.Ratio);
        if (total <= 0) return null;
        var roll = nextBelow(total);
        var passed = 0;
        foreach (var zone in entry.Zones)
        {
            passed += zone.Ratio;
            if (roll < passed) return zone.ZoneId;
        }
        return null;
    }
}

public static class HairDyeRules
{
    public const int ColorCount = 0x15;
    public const int BaseSlot = 1;
    public const int DataBytes = 1;

    public static bool ValidColor(byte color) => color < ColorCount;

    public static int SlotOf(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return Math.Clamp((int)character.JobState, BaseSlot, OccupationRules.ThirdJobTier);
    }
}

public static class GenderChangeRules
{
    public const int DataBytes = 2;
    public const int DataLimit = 9;
    public const int FirstChangedJob = 9;
    public const int LastType = 8;
    public const int ClearedHair = 0;
    public const int GrantedItems = 2;

    public static byte? OppositeType(int type) => type switch
    {
        < 1 or > LastType => null,
        _ => (byte)(type % 2 == 1 ? type + 1 : type - 1),
    };

    public static bool HasChangedJob(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return OccupationRules.CurrentJobOf(character) >= FirstChangedJob || character.Job2 != 0 || character.Job3 != 0;
    }

    public static int BagSlotsNeeded(int wornCount) => wornCount + GrantedItems;
}

public static class StatResetRules
{
    public const int StatCount = 12;
    public const int StatsPerGroup = 3;
    public const int GroupCount = 4;
    public const int PartialMinimum = 1;
    public const int PartialMaximum = 40;
    public const int PartialDataBytes = StatCount;
    public const int GrowthDataBytes = GroupCount;
    public const int GrowthWeightMinimum = 1;
    public const int GrowthWeightMaximum = 4;
    public const int GrowthWeightTotal = 10;
    public const int MainGrowthWeight = 4;
    public const int BonusPerLevel = 4;
    public const int PointsPerLevel = StatsPerGroup * GrowthWeightTotal + BonusPerLevel;

    public static int GroupOf(int stat) => stat / StatsPerGroup;

    public static int Baseline(int level, int weight) => (level - 1) * weight;

    public static bool TryPartial(IReadOnlyList<byte> take, IReadOnlyList<short> points, IReadOnlyList<short> weights, int level, out short[] reset, out int refunded)
    {
        ArgumentNullException.ThrowIfNull(take);
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(weights);
        reset = new short[StatCount];
        refunded = 0;
        var total = take.Sum(amount => (int)amount);
        if (total < PartialMinimum || total > PartialMaximum) return false;
        for (var stat = 0; stat < StatCount; stat++)
        {
            var left = points[stat] - take[stat];
            if (left < Baseline(level, weights[GroupOf(stat)])) return false;
            reset[stat] = (short)left;
        }
        refunded = total;
        return true;
    }

    public static bool TryAll(IReadOnlyList<short> points, IReadOnlyList<short> weights, int level, out short[] reset, out int refunded)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(weights);
        reset = new short[StatCount];
        refunded = 0;
        for (var stat = 0; stat < StatCount; stat++)
        {
            var baseline = Baseline(level, weights[GroupOf(stat)]);
            if (points[stat] < baseline) return false;
            reset[stat] = (short)baseline;
            refunded += points[stat] - baseline;
        }
        return refunded > 0;
    }

    public static bool ValidGrowthWeights(IReadOnlyList<byte> weights, int mainGroup)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (weights.Count != GroupCount || mainGroup < 0 || mainGroup >= GroupCount) return false;
        if (weights.Any(weight => weight < GrowthWeightMinimum || weight > GrowthWeightMaximum)) return false;
        return weights.Sum(weight => (int)weight) == GrowthWeightTotal && weights[mainGroup] == MainGrowthWeight;
    }

    public static short[] GrowthPoints(IReadOnlyList<byte> weights, int level)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var points = new short[StatCount];
        for (var stat = 0; stat < StatCount; stat++) points[stat] = (short)Baseline(level, weights[GroupOf(stat)]);
        return points;
    }

    public static int GrowthBonus(int level) => (level - 1) * BonusPerLevel;

    public static bool GrowthConsistent(IReadOnlyList<short> points, int bonus, int level)
    {
        ArgumentNullException.ThrowIfNull(points);
        return points.Sum(point => (int)point) + bonus == (level - 1) * PointsPerLevel;
    }
}

public static class SkillResetRules
{
    public const int DataBytes = 2;
    public const int FirstSpecialSkill = 5000;
    public const int MasteredLevel = 11;

    public static bool IsSpecial(int skillId) => skillId >= FirstSpecialSkill;

    public static bool IsPrerequisiteOfOwned(int skillId, IEnumerable<int> ownedSkills, Func<int, SkillCardEntry?> cardOfSkill)
    {
        ArgumentNullException.ThrowIfNull(ownedSkills);
        ArgumentNullException.ThrowIfNull(cardOfSkill);
        return ownedSkills.Any(owned => owned != skillId && cardOfSkill(owned) is { } card && card.PreSkills.Any(pre => pre.SkillId == skillId));
    }

    public static int Refund(SkillCardEntry card, int level, int masterTmPoint)
    {
        ArgumentNullException.ThrowIfNull(card);
        return card.AffectionPoint + (level - card.RegisterLevel) + (level == MasteredLevel ? masterTmPoint - 1 : 0);
    }
}

public static class StorageExpansionRules
{
    public const int SlotsPerItem = 8;
    public const int MaximumSlots = 800;

    public static bool Fits(int currentSlots) => currentSlots + SlotsPerItem <= MaximumSlots;
}
