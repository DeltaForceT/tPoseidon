namespace tPoseidon.Core.Game.Items;

public readonly record struct ItemPriceStatistics(int Minimum, int Maximum, int Average);

public sealed class ItemPriceBoard
{
    private sealed class Node
    {
        public int Count;
        public long Sum;
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
                _nodes[itemId] = new Node { Count = 1, Sum = price, Minimum = price, Maximum = price };
                return;
            }
            node.Count++;
            node.Sum += price;
            node.Minimum = Math.Min(node.Minimum, price);
            node.Maximum = Math.Max(node.Maximum, price);
        }
    }

    public ItemPriceStatistics? Find(uint itemId)
    {
        lock (_gate)
        {
            return _nodes.TryGetValue(itemId, out var node)
                ? new ItemPriceStatistics(node.Minimum, node.Maximum, (int)(node.Sum / node.Count))
                : null;
        }
    }
}
