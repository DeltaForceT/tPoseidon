using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Soul;

public readonly record struct SoulTick(GuardianSoulRow State, int SourcesConsumed, int PotionsMade);

public readonly record struct SoulCharge(int Added, short Remaining);

public static class SoulRules
{
    public const int LevelType = 0x10;
    public const int LevelId = 28001;
    public const int RemainType = 0x11;
    public const int RemainId = 28000;
    public const int MaxSoulLevel = 10;
    public const int CapLevelOffset = 3;
    public const int CapStep = 100;
    public const int MaterialSlots = 5;
    public const int PotionSubType = 84;

    public const byte Ok = 0;

    public const byte NotifyBadItem = 1;
    public const byte NotifyNotEnoughSource = 2;
    public const byte NotifyBadPotionInfo = 3;
    public const byte NotifyBagFull = 4;
    public const byte NotifyOverWeight = 5;
    public const byte NotifyBadPotionCount = 6;
    public const byte NotifyBadGuardian = 7;
    public const byte NotifyRetry = 8;

    public const byte WindowState = 1;
    public const byte ImpossibleItem = 2;
    public const byte MaterialMissing = 3;
    public const byte SoulShort = 4;
    public const byte Unknown = 5;

    public const byte ChargeState = 1;
    public const byte ChargeImpossibleItem = 2;
    public const byte ChargeSoulShort = 3;

    public const byte PotionCannotUse = 1;

    public static GuardianSoulRow Fresh(ulong guardianUid, DateTimeOffset now) => new(guardianUid, 0, 0, 0, 0, now);

    public static bool HasSource(GuardianSoulRow state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.SourceItemUid != 0 && state.SourceCount > 0;
    }

    public static GuardianSoulRow WithoutSource(GuardianSoulRow state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state with { SourceItemUid = 0, SourceCount = 0 };
    }

    public static int? GaugeUpOf(IEnumerable<SoulSourceEntry> sources, int guardianType, int sourceItemId)
    {
        ArgumentNullException.ThrowIfNull(sources);
        return sources.FirstOrDefault(source => source.GuardianType == guardianType && source.SourceItemId == sourceItemId)?.GaugeUp;
    }

    public static int PotionsPerGauge(IReadOnlyList<SoulPotionCountEntry> counts, int guardianLevel, bool online)
    {
        ArgumentNullException.ThrowIfNull(counts);
        if (!online || counts.Count == 0) return 1;
        var row = counts.FirstOrDefault(entry => guardianLevel >= entry.GuardianLevelMin && guardianLevel <= entry.GuardianLevelMax)
            ?? counts.MaxBy(entry => entry.GuardianLevelMax)!;
        return row.Count;
    }

    public static byte CheckRegister(bool hasGuardian, bool inBag, bool isSource, int count, int stack, bool alreadyRegistered)
    {
        if (!hasGuardian) return NotifyBadGuardian;
        if (!inBag || !isSource) return NotifyBadItem;
        if (count <= 0 || count > stack) return NotifyNotEnoughSource;
        return alreadyRegistered ? NotifyBadItem : Ok;
    }

    public static SoulTick Tick(GuardianSoulRow state, SoulSystemInfoEntry info, int? sourceGaugeUp, int sourceStack, int potionsPerGauge)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(info);
        var next = state with { LastTick = state.LastTick.AddMinutes(info.GaugeMinutes) };
        var consumed = 0;
        if (HasSource(next) && sourceGaugeUp is { } gaugeUp && sourceStack >= info.SourceDecreaseCount && next.SourceCount >= info.SourceDecreaseCount)
        {
            consumed = info.SourceDecreaseCount;
            var left = next.SourceCount - consumed;
            next = next with { Gauge = next.Gauge + gaugeUp, SourceCount = left, SourceItemUid = left > 0 ? next.SourceItemUid : 0 };
        }
        else
        {
            next = WithoutSource(next) with { Gauge = Math.Max(0, next.Gauge - info.GaugeDecrease) };
        }
        var made = 0;
        while (info.GaugeMax > 0 && next.Gauge >= info.GaugeMax)
        {
            next = next with { Gauge = next.Gauge - info.GaugeMax };
            made += potionsPerGauge;
        }
        return new SoulTick(next with { PotionsReady = (int)Math.Min(ushort.MaxValue, (long)next.PotionsReady + made) }, consumed, made);
    }

    public static bool TickDue(GuardianSoulRow state, SoulSystemInfoEntry info, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(info);
        return info.GaugeMinutes > 0 && state.LastTick.AddMinutes(info.GaugeMinutes) <= now;
    }

    public static byte CheckTakePotions(int count, int ready) => count <= 0 || count > ready ? NotifyBadPotionCount : Ok;

    public static short PropertyValue(IReadOnlyList<ItemProperty> properties, int type, int id)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return (short)properties.Where(property => property.Type == type && property.Id == id).Sum(property => property.Value);
    }

    public static IReadOnlyList<ItemProperty> WithProperty(IReadOnlyList<ItemProperty> properties, int type, int id, short value)
    {
        ArgumentNullException.ThrowIfNull(properties);
        var kept = properties.Where(property => !(property.Type == type && property.Id == id)).ToList();
        kept.Add(new ItemProperty(type, id, value, 0));
        return kept;
    }

    public static int ChargeCap(int soulLevel) => (soulLevel + CapLevelOffset) * CapStep;

    public static byte CheckCharge(bool windowOpen, bool soulItem, uint amount, uint soul)
    {
        if (!windowOpen) return ChargeState;
        if (!soulItem) return ChargeImpossibleItem;
        return amount == 0 || amount > soul ? ChargeSoulShort : Ok;
    }

    public static SoulCharge Charge(int soulLevel, int remaining, uint amount)
    {
        var cap = ChargeCap(soulLevel);
        var target = (int)Math.Min(cap, (long)Math.Max(0, remaining) + amount);
        var added = Math.Max(0, target - Math.Max(0, remaining));
        return new SoulCharge(added, (short)Math.Max(remaining, target));
    }

    public static bool HasMaterials(IReadOnlyList<SoulMaterial> required, IReadOnlyList<Item> offered)
    {
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(offered);
        return required.All(material => offered.Where(item => item.ItemIndex == material.ItemId).Sum(item => (int)item.Stack) >= material.Count);
    }

    public static IReadOnlyList<(Item Item, int Count)> Consumption(IReadOnlyList<SoulMaterial> required, IReadOnlyList<Item> offered)
    {
        ArgumentNullException.ThrowIfNull(required);
        ArgumentNullException.ThrowIfNull(offered);
        var takes = new List<(Item Item, int Count)>();
        foreach (var material in required)
        {
            var left = material.Count;
            foreach (var item in offered.Where(item => item.ItemIndex == material.ItemId))
            {
                if (left <= 0) break;
                var take = Math.Min(left, (int)item.Stack);
                takes.Add((item, take));
                left -= take;
            }
        }
        return takes;
    }

    public static byte CheckCraft(bool windowOpen, SoulCraftEntry? recipe, bool hasMaterials, uint soul)
    {
        if (!windowOpen) return WindowState;
        if (recipe is null) return ImpossibleItem;
        if (!hasMaterials) return MaterialMissing;
        if (soul < recipe.Fee) return SoulShort;
        return recipe.Probability < 1f ? Unknown : Ok;
    }

    public static SoulIntensifyEntry? IntensifyRow(IReadOnlyList<SoulIntensifyEntry> rows, int nextLevel, int weaponLevel)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows.FirstOrDefault(row => row.SoulLevel == nextLevel && row.WeaponLevel == weaponLevel);
    }

    public static byte CheckIntensify(bool windowOpen, bool soulItem, int soulLevel, SoulIntensifyEntry? row, bool hasMaterials, uint soul)
    {
        if (!windowOpen) return WindowState;
        if (!soulItem || soulLevel >= MaxSoulLevel || row is null) return ImpossibleItem;
        if (!hasMaterials) return MaterialMissing;
        return soul < row.Cost ? SoulShort : Ok;
    }

    public static short Drain(short remaining, int decrease) => (short)Math.Max(0, remaining - Math.Max(0, decrease));
}
