using System.Globalization;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting.GameMaster;

public sealed record GmGiveItemChoice(bool BoxAvailable, bool BoxByDefault)
{
    public static readonly GmGiveItemChoice Unknown = new(true, false);

    public static GmGiveItemChoice For(ItemName? item) =>
        item is null
            ? Unknown
            : new GmGiveItemChoice(CashBoxGrantRules.Accepts(item.Definition.ObjectClass), item.Table == ItemNames.CashTable && CashBoxGrantRules.Accepts(item.Definition.ObjectClass));
}

public static class GmGiveItem
{
    public const string BagCommand = "present";
    public const string BoxCommand = "present-box";

    public static string CommandFor(bool toCashBox) => toCashBox ? BoxCommand : BagCommand;

    public static IReadOnlyList<string> Arguments(string name, string itemId, string count, bool toCashBox, string hours)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentNullException.ThrowIfNull(count);
        ArgumentNullException.ThrowIfNull(hours);
        return toCashBox ? [name, itemId, count, hours.Length == 0 ? "0" : hours] : hours.Length == 0 ? [name, itemId, count] : [name, itemId, count, hours];
    }

    public static bool HoursValid(string hours) =>
        hours.Length == 0 || ushort.TryParse(hours, NumberStyles.None, CultureInfo.InvariantCulture, out _);
}
