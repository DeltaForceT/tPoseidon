namespace tPoseidon.Core.Net.World;

public readonly record struct GunShotRequest(ushort TargetEntityId, ushort CheckWord, ushort BulletsPerShot, byte MoveType)
{
    public bool Charged => (CheckWord & ChargedFlag) != 0;

    public const ushort ChargedFlag = 1;
}

public static class GunFrames
{
    public const int ShotRequestSize = 7;
    public const ushort KeyMask = 0xFFFE;
    private const int RefusedSize = 4;

    public static bool TryReadShot(ReadOnlySpan<byte> payload, out GunShotRequest request)
    {
        request = default;
        if (payload.Length < ShotRequestSize) return false;
        var r = new PacketReader(payload);
        request = new GunShotRequest(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadByte());
        return true;
    }

    public static byte[] Key(ushort key)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16((ushort)(key & KeyMask));
        return w.ToArray();
    }

    public static byte[] Refused() => new byte[RefusedSize];
}
