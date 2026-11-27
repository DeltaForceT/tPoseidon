using System.Globalization;

namespace tPoseidon.Core.Game.Items;

public readonly record struct ItemPriceStatistics(int Minimum, int Maximum, int Average);

public sealed class ItemPriceBoard
{
    public const long CompactionThreshold = 100_000_000_000L;
    public const int CompactionDivisor = 100;

    private sealed class Node
    {
        public int Count;
        public long Sum;
        public int Average;
        public int Minimum;
        public int Maximum;
    }

    private readonly object _gate = new();
    private readonly Dictionary<uint, Node> _nodes = new();

    public void Record(uint itemId, int price)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(itemId, out var node))
            {
                _nodes[itemId] = new Node { Count = 1, Sum = price, Average = price, Minimum = price, Maximum = price };
                return;
            }
            node.Count++;
            node.Sum += price;
            node.Minimum = Math.Min(node.Minimum, price);
            node.Maximum = Math.Max(node.Maximum, price);
            node.Average = (int)(node.Sum / node.Count);
        }
    }

    public ItemPriceStatistics? Find(uint itemId)
    {
        lock (_gate)
        {
            return _nodes.TryGetValue(itemId, out var node)
                ? new ItemPriceStatistics(node.Minimum, node.Maximum, node.Average)
                : null;
        }
    }

    public int Load(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var loaded = new Dictionary<uint, Node>();
        var lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length == 0) continue;
            var fields = line.Split('|');
            if (fields.Length != 6
                || !uint.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var itemId)
                || !long.TryParse(fields[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var sum)
                || !int.TryParse(fields[2], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var count)
                || !int.TryParse(fields[3], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var average)
                || !int.TryParse(fields[4], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var minimum)
                || !int.TryParse(fields[5], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var maximum))
            {
                throw new InvalidDataException($"item price line {lineNumber} is not id|sum|count|average|minimum|maximum");
            }
            loaded[itemId] = new Node { Count = count, Sum = sum, Average = average, Minimum = minimum, Maximum = maximum };
        }
        lock (_gate)
        {
            foreach (var (itemId, node) in loaded) _nodes[itemId] = node;
        }
        return loaded.Count;
    }

    public int Save(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        lock (_gate)
        {
            foreach (var (itemId, node) in _nodes.OrderBy(pair => pair.Key))
            {
                if (node.Sum > CompactionThreshold)
                {
                    node.Sum /= CompactionDivisor;
                    node.Count = Math.Max(1, node.Count / CompactionDivisor);
                }
                writer.Write(string.Create(CultureInfo.InvariantCulture, $"{itemId}|{node.Sum}|{node.Count}|{node.Average}|{node.Minimum}|{node.Maximum}\n"));
            }
            return _nodes.Count;
        }
    }
}
