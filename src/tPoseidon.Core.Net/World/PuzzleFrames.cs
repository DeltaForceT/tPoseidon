using tPoseidon.Core.Game.Puzzle;

namespace tPoseidon.Core.Net.World;

public static class PuzzleFrames
{
    public static byte[] SlideStart(int slideId, (int X, int Y) origin, IReadOnlyList<int> grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        var w = new PacketWriter(8 + grid.Count * 2);
        w.WriteInt16((short)slideId);
        w.WriteInt16((short)origin.X);
        w.WriteInt16((short)origin.Y);
        w.WriteUInt16((ushort)grid.Count);
        foreach (var block in grid) w.WriteUInt16((ushort)block);
        return w.ToArray();
    }

    public static byte[] SlideMove(SlideMove move)
    {
        var w = new PacketWriter(4);
        w.WriteInt16((short)move.From);
        w.WriteInt16((short)move.To);
        return w.ToArray();
    }

    public static byte[] Result(uint result, uint record)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32(result);
        w.WriteUInt32(record);
        return w.ToArray();
    }
}
