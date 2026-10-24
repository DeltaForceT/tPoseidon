namespace tPoseidon.Core.Net.World;

public static class CardIdentifyFrames
{
    public const ushort RefusesSkills = 0x0A;
    public const uint NoScore = 0;
    public const int PositionSize = sizeof(uint);

    public static byte[] Ended(uint score, uint exp, uint tmExp)
    {
        var w = new PacketWriter(3 * sizeof(uint));
        w.WriteUInt32(score);
        w.WriteUInt32(exp);
        w.WriteUInt32(tmExp);
        return w.ToArray();
    }

    public static bool TryReadCard(ReadOnlySpan<byte> payload, out uint cardItemId)
    {
        cardItemId = 0;
        if (payload.Length < sizeof(uint)) return false;
        cardItemId = new PacketReader(payload).ReadUInt32();
        return true;
    }

    public static bool TryReadPosition(ReadOnlySpan<byte> payload, out int position)
    {
        position = 0;
        if (payload.Length < PositionSize) return false;
        position = (int)new PacketReader(payload).ReadUInt32();
        return true;
    }

    public static bool TryReadPair(ReadOnlySpan<byte> payload, out int first, out int second)
    {
        first = 0;
        second = 0;
        if (payload.Length < 2 * PositionSize) return false;
        var r = new PacketReader(payload);
        first = (int)r.ReadUInt32();
        second = (int)r.ReadUInt32();
        return true;
    }

    public static byte[] Life(short life)
    {
        var w = new PacketWriter(2);
        w.WriteInt16(life);
        return w.ToArray();
    }

    public static byte[] Dealt(short stage, int specialPosition)
    {
        var w = new PacketWriter(6);
        w.WriteInt16(stage);
        w.WriteInt32(specialPosition);
        return w.ToArray();
    }

    public static byte[] Flipped(uint itemId, uint group, ushort position)
    {
        var w = new PacketWriter(10);
        w.WriteUInt32(itemId);
        w.WriteUInt32(group);
        w.WriteUInt16(position);
        return w.ToArray();
    }

    public static byte[] Matched(uint itemId, short combo, uint score, bool droppedOnGround)
    {
        var w = new PacketWriter(11);
        w.WriteUInt32(itemId);
        w.WriteInt16(combo);
        w.WriteUInt32(score);
        w.WriteByte(droppedOnGround ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] Taken(uint itemId, bool ok)
    {
        var w = new PacketWriter(5);
        w.WriteUInt32(itemId);
        w.WriteByte(ok ? (byte)1 : (byte)0);
        return w.ToArray();
    }
}
