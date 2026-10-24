using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.Net.World;

public static class LookChangeFrames
{
    public const int LookCount = 6;
    public const int RequestSize = 2;

    public static byte[] Looks(IReadOnlyList<ushort> looks)
    {
        ArgumentNullException.ThrowIfNull(looks);
        var w = new PacketWriter(LookCount * sizeof(ushort));
        for (var i = 0; i < LookCount; i++) w.WriteUInt16(i < looks.Count ? looks[i] : (ushort)0);
        return w.ToArray();
    }

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out ushort state)
    {
        state = 0;
        if (payload.Length < RequestSize) return false;
        state = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static byte[] Changed(ushort entityId, MakeUp makeUp, byte tier)
    {
        var w = new PacketWriter(8);
        w.WriteUInt16(entityId);
        w.WriteUInt16(makeUp.Type);
        w.WriteByte(makeUp.Job);
        w.WriteByte(makeUp.Hair);
        w.WriteByte(tier);
        w.WriteByte(LookChangeRules.ChangedByAppearance);
        return w.ToArray();
    }
}
