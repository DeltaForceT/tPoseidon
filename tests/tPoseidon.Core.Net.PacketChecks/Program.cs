using System.Buffers.Binary;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Net.World;

// Synthetic values only; no client assets or captured packet bytes.
foreach (var itemClass in new byte[] { 0, 1, 2 })
{
    foreach (var hidden in new[] { false, true })
    {
        var item = new FieldItem(0x1234, 1, 0x2345, 0x3456, 12345, 7,
            itemClass, false, 0, 0, DateTimeOffset.UnixEpoch) { IsHidden = hidden };
        var ordinary = GroundItemFrames.AlreadyThere(item);
        var treasure = DrillFrames.TreasureItem(item);
        var expectedLength = itemClass == 2 ? 28 : 24;
        if (treasure.Length != expectedLength
            || !treasure.AsSpan(0, 6).SequenceEqual(ordinary.AsSpan(0, 6))
            || !treasure.AsSpan(6).SequenceEqual(ordinary.AsSpan(8))
            || treasure[7] != 0x80 || treasure[9] != itemClass
            || BinaryPrimitives.ReadUInt32LittleEndian(treasure.AsSpan(10)) != 12345
            || BinaryPrimitives.ReadUInt16LittleEndian(ordinary.AsSpan(6)) != (hidden ? 1 : 0)
            || GroundItemFrames.ReadAlreadyThere(ordinary).IsHidden != hidden)
        {
            throw new InvalidOperationException($"Treasure or ordinary ground-item layout mismatch: class {itemClass}, hidden {hidden}.");
        }
        // Two consecutive records must retain their individual boundaries.
        var batch = treasure.Concat(treasure).ToArray();
        if (BinaryPrimitives.ReadUInt16LittleEndian(batch.AsSpan(expectedLength)) != item.EntityId)
        {
            throw new InvalidOperationException("Treasure batch record boundary mismatch.");
        }
    }
}
Console.WriteLine("PASS: treasure layouts, ordinary hidden-state preservation, rare extension and record boundaries.");
