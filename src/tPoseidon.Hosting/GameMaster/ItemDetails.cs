using System.Globalization;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting.GameMaster;

public sealed record ItemDetailLine(string Label, string Value);

public sealed record ItemDetails(string Title, IReadOnlyList<ItemDetailLine> Lines);

public sealed record ItemDetailData(
    IReadOnlyDictionary<string, string> Words,
    IReadOnlyDictionary<int, DrillParamEntry> Drills,
    CharacterTypeNames CharacterTypes,
    IReadOnlyDictionary<int, int> TimerMinutes)
{
    public static ItemDetailData Empty { get; } = new(new Dictionary<string, string>(), new Dictionary<int, DrillParamEntry>(), CharacterTypeNames.Empty, new Dictionary<int, int>());
}

public static class ItemDetailBuilder
{
    public const string StatWordPrefix = "ChrStatus_";

    private const int StatCount = EquipmentRules.ItemStatCount;

    private static readonly string[] StatCodes = ["AP", "AC", "DX", "MaxMP", "MA", "MD", "MaxWT", "DA", "LK", "MaxHP", "DP", "HV"];

    public static ItemDetails Build(ItemName item, ItemDetailData data)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(data);
        var words = data.Words;
        var drill = data.Drills.GetValueOrDefault(item.Id);
        var definition = item.Definition;
        var lines = new List<ItemDetailLine>
        {
            new("Item id", Number(item.Id)),
            new("Game data table", item.Table),
            new("Category", $"{ItemCategories.Of(definition).Name} (type {Number(definition.Type)}, subtype {Number(definition.SubType)})"),
        };
        if (SlotName(definition.Type) is { } slot)
        {
            lines.Add(new("Worn on", slot));
        }

        if (definition.MinLevel > 0)
        {
            lines.Add(new("Level required", Number(definition.MinLevel)));
        }

        if (definition.MinStatLevel != 0 && definition.MinStatType is >= 0 and < StatCount)
        {
            lines.Add(new("Stat required", $"{StatName(words, definition.MinStatType)} {Number(definition.MinStatLevel)}"));
        }

        if (CharacterTypes(definition, data.CharacterTypes) is { } types)
        {
            lines.Add(new("Character types", types));
        }

        for (var index = 0; index < StatCount && index < definition.StatBonuses.Count; index++)
        {
            if (definition.StatBonuses[index] != 0)
            {
                lines.Add(new(StatName(words, index), Signed(definition.StatBonuses[index])));
            }
        }

        if (definition.SubType == ItemParamEntry.GunSubType && definition.Ap != 0)
        {
            lines.Add(new($"Gun {StatName(words, 0)}", Number(definition.Ap)));
        }

        if (definition.HpRecoveryRate != 0)
        {
            lines.Add(new("HP recovery rate", definition.HpRecoveryRate.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        if (definition.MpRecoveryRate != 0)
        {
            lines.Add(new("MP recovery rate", definition.MpRecoveryRate.ToString("0.##", CultureInfo.InvariantCulture)));
        }

        lines.Add(new("Stack", definition.IsStackable ? "Stackable" : "One item per slot"));
        if (data.TimerMinutes.TryGetValue(item.Id, out var minutes) && minutes > 0)
        {
            lines.Add(new("Time limit", Duration(minutes)));
        }

        var options = Options(definition);
        if (options.Count > 0)
        {
            lines.Add(new("Options", string.Join(", ", options)));
        }

        if (definition.ReformCount > 0)
        {
            lines.Add(new("Reforms", Number(definition.ReformCount)));
        }

        if (definition.CompoundSlot > 0)
        {
            lines.Add(new("Compound slots", Number(definition.CompoundSlot)));
        }

        lines.Add(new("Weight", Number(definition.Weight)));
        lines.Add(new("Value", Number(definition.Value)));
        if (definition.Type == ShopPrices.DrillType && drill is not null)
        {
            lines.Add(new("Drill level", Number(drill.Grade)));
            lines.Add(new("Drill depth", Number(drill.DigDepth)));
            lines.Add(new("Drill durability", Number(drill.DrillLife)));
            lines.Add(new("Ground nature", Number(drill.GroundNature)));
            lines.Add(new("Bar area", Number(drill.BarEffArea)));
            lines.Add(new("Bar speed", Number(drill.BarSpeed)));
            lines.Add(new("Scoop mass", Number(drill.ScoopMass)));
        }

        return new ItemDetails(item.Name, lines);
    }

    public static ItemDetails Build(MyCampItemEntry item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return new ItemDetails(item.Name, [new("Item id", Number(item.Id)), new("Game data table", MyCampItemEntry.TableName)]);
    }

    public static string StatName(IReadOnlyDictionary<string, string> words, int index) =>
        words.TryGetValue(StatWordPrefix + Number(index), out var name) && name.Length > 0 ? name : StatCodes[index];

    private static string? SlotName(int type)
    {
        var slot = EquipmentRules.SlotOfType(type);
        if (slot == EquipmentRules.NoSlot)
        {
            return null;
        }

        return slot switch
        {
            EquipmentSlot.Hat => "Hat",
            EquipmentSlot.Weapon => "Weapon",
            EquipmentSlot.Shield => "Shield",
            EquipmentSlot.Belt => "Belt",
            EquipmentSlot.Accessory => "Accessory",
            EquipmentSlot.Ear => "Ears",
            EquipmentSlot.Tail => "Tail",
            EquipmentSlot.Drill => "Drill",
            EquipmentSlot.Pet => "Pet",
            EquipmentSlot.Shoes => "Shoes",
            EquipmentSlot.Socks => "Socks",
            EquipmentSlot.Cape => "Cape",
            _ => $"Costume {Number(slot - EquipmentSlot.FirstCostume + 1)}",
        };
    }

    public static string Duration(int minutes)
    {
        var parts = new List<string>();
        var span = TimeSpan.FromMinutes(minutes);
        if (span.Days > 0) parts.Add(Plural(span.Days, "day"));
        if (span.Hours > 0) parts.Add(Plural(span.Hours, "hour"));
        if (span.Minutes > 0) parts.Add(Plural(span.Minutes, "minute"));
        return string.Join(" ", parts);
    }

    private static string Plural(int count, string unit) => $"{Number(count)} {unit}{(count == 1 ? string.Empty : "s")}";

    private static string? CharacterTypes(ItemParamEntry definition, CharacterTypeNames names)
    {
        if (!EquipmentRules.RestrictsCharacterType(definition))
        {
            return null;
        }

        var mask = EquipmentRules.CharacterTypeMask(definition);
        if ((mask & EquipmentRules.AllCharacterTypes) == EquipmentRules.AllCharacterTypes)
        {
            return null;
        }

        var allowed = Enumerable.Range(1, EquipmentRules.CharacterTypeCount).Where(type => EquipmentRules.FitsCharacterType(mask, type)).ToList();
        if (allowed.Count == 0)
        {
            return "none";
        }

        return string.Join("; ", allowed
            .GroupBy(CharacterTypeNames.SpeciesOf)
            .OrderBy(group => group.Key)
            .Select(group => $"{names.Species.GetValueOrDefault(group.Key) ?? "species " + Number(group.Key)}: {string.Join(", ", group.OrderBy(type => type).Select(type => names.Jobs.GetValueOrDefault(type) ?? "type " + Number(type)))}"));
    }

    private static List<string> Options(ItemParamEntry definition)
    {
        var options = new List<string>();
        if (definition.IsUsable) options.Add("usable");
        if ((definition.Options & ShopPrices.NotSellableOption) != 0) options.Add("cannot be sold to a shop");
        if (definition.OnlyDropperTakes) options.Add("only the dropper can pick it up");
        if (definition.IsKeptInBag) options.Add("stays in the bag");
        if (definition.IsKeptWhenUsed) options.Add("kept when used");
        if (definition.HasRandomStats) options.Add("random stats");
        if ((definition.ExtendedOptions & EquipmentRules.GmOnlyExtendedOption) != 0) options.Add("GM only");
        if ((definition.Options & ItemParamEntry.NotReformOption) != 0) options.Add("cannot be reformed");
        return options;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Signed(int value) => value.ToString("+0;-0", CultureInfo.InvariantCulture);
}
