using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public readonly record struct RolledDrop(int ItemId, int Count);

public sealed class MonsterDropRoller
{
    public const int MostDrops = 10;
    public const int RollRange = 10000;

    public const int FirstMoneyItem = 200;
    public const int FullMoneyStack = 10000;

    private static readonly int[] MoneyItemLimits = { 50, 500, 2000, FullMoneyStack };

    private readonly Dictionary<int, DropRow> _rows = new();
    private readonly Dictionary<int, DropTable> _tables = new();

    public MonsterDropRoller(IEnumerable<MonItemDropInfoEntry> rows, IEnumerable<MonItemTableDropEntry> tables)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(tables);
        foreach (var row in rows) _rows[row.Id] = BuildRow(row);
        foreach (var table in tables) _tables[table.Id] = BuildTable(table);
    }

    public int RowCount => _rows.Count;

    public int TableCount => _tables.Count;

    public IReadOnlyList<RolledDrop> Roll(int dropIndex, int rolls, Func<int, bool> dropsOneOnly, Random rng)
    {
        ArgumentNullException.ThrowIfNull(dropsOneOnly);
        ArgumentNullException.ThrowIfNull(rng);
        var drops = new List<RolledDrop>();
        if (!_rows.TryGetValue(dropIndex, out var row)) return drops;

        for (var roll = 0; roll < rolls; roll++)
        {
            var value = rng.Next(RollRange);
            var hit = false;
            foreach (var slot in row.Items)
            {
                if (slot.Upper == 0) break;
                if (value < slot.Upper)
                {
                    drops.Add(new RolledDrop(slot.ItemId, dropsOneOnly(slot.ItemId) ? 1 : slot.Count));
                    hit = true;
                    break;
                }
            }
            if (drops.Count >= MostDrops) return drops;
            if (!hit) RollTables(row, value, rolls, drops, dropsOneOnly, rng);
        }
        return drops;
    }

    public static int? MoneyAmount(double gelderRate, int minimum, int maximum, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        var value = rng.Next(RollRange);
        if ((int)((double)(float)gelderRate * RollRange) <= value) return null;
        if (minimum == 0 && maximum == 0) return null;
        if (maximum < minimum) (minimum, maximum) = (maximum, minimum);
        return maximum == minimum ? minimum : minimum + rng.Next(maximum - minimum);
    }

    public static IReadOnlyList<RolledDrop> SplitMoney(int amount)
    {
        var remainder = amount % FullMoneyStack;
        if (remainder == 0) return Array.Empty<RolledDrop>();

        var items = new List<RolledDrop>();
        var lastMoneyItem = FirstMoneyItem + MoneyItemLimits.Length - 1;
        for (var full = 0; full < amount / FullMoneyStack; full++)
        {
            items.Add(new RolledDrop(lastMoneyItem, FullMoneyStack));
        }
        for (var kind = 0; kind < MoneyItemLimits.Length; kind++)
        {
            var above = kind == 0 ? int.MinValue : MoneyItemLimits[kind - 1];
            if (above < remainder && remainder <= MoneyItemLimits[kind])
            {
                items.Add(new RolledDrop(FirstMoneyItem + kind, remainder));
                return items;
            }
        }
        return Array.Empty<RolledDrop>();
    }

    private void RollTables(
        DropRow row, int value, int rolls, List<RolledDrop> drops, Func<int, bool> dropsOneOnly, Random rng)
    {
        var passes = rolls - drops.Count;
        for (var pass = 0; pass < passes; pass++)
        {
            foreach (var slot in row.Tables)
            {
                if (slot.Upper == 0) break;
                if (value < slot.Upper)
                {
                    if (_tables.TryGetValue(slot.TableId, out var table))
                    {
                        RollTable(table, rolls, drops, dropsOneOnly, rng);
                    }
                    break;
                }
            }
            if (drops.Count >= MostDrops) return;
        }
    }

    private static void RollTable(
        DropTable table, int rolls, List<RolledDrop> drops, Func<int, bool> dropsOneOnly, Random rng)
    {
        if (table.Total < 1) return;
        var passes = rolls - drops.Count;
        for (var pass = 0; pass < passes; pass++)
        {
            var value = rng.Next(table.Total);
            foreach (var slot in table.Items)
            {
                if (slot.Upper == 0) break;
                if (value < slot.Upper)
                {
                    drops.Add(new RolledDrop(slot.ItemId, dropsOneOnly(slot.ItemId) ? 1 : slot.Count));
                    break;
                }
            }
            if (drops.Count >= MostDrops) return;
        }
    }

    private static DropRow BuildRow(MonItemDropInfoEntry row)
    {
        var total = 0f;
        foreach (var item in row.Items) total += item.Rate;
        foreach (var table in row.SubTables) total += table.Rate;

        var upper = 0;
        var items = new ItemSlot[row.Items.Count];
        for (var i = 0; i < items.Length; i++)
        {
            upper += Share(row.Items[i].Rate, total);
            items[i] = new ItemSlot(row.Items[i].ItemId, row.Items[i].Count, upper);
        }
        var tables = new TableSlot[row.SubTables.Count];
        for (var i = 0; i < tables.Length; i++)
        {
            upper += Share(row.SubTables[i].Rate, total);
            tables[i] = new TableSlot(row.SubTables[i].TableId, upper);
        }
        return new DropRow(items, tables);
    }

    private static DropTable BuildTable(MonItemTableDropEntry table)
    {
        var used = 0;
        var sum = 0f;
        foreach (var slot in table.Slots)
        {
            if (slot.ItemId == 0) continue;
            used++;
            sum += slot.Rate;
        }

        var normalised = sum > 1f;
        var upper = 0;
        var items = new ItemSlot[Math.Min(used, table.Slots.Count)];
        for (var i = 0; i < items.Length; i++)
        {
            var slot = table.Slots[i];
            upper += Share(slot.Rate, normalised ? sum : 0f);
            items[i] = new ItemSlot(slot.ItemId, slot.Count, upper);
        }
        return new DropTable(items, normalised ? RollRange : (int)((double)sum * RollRange));
    }

    private static int Share(float rate, float total)
    {
        var share = total > 1f ? (float)((double)rate / total) : rate;
        return (int)((double)share * RollRange);
    }

    private readonly record struct ItemSlot(int ItemId, int Count, int Upper);

    private readonly record struct TableSlot(int TableId, int Upper);

    private sealed record DropRow(ItemSlot[] Items, TableSlot[] Tables);

    private sealed record DropTable(ItemSlot[] Items, int Total);
}
