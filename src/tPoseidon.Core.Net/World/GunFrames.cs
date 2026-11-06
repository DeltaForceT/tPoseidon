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
    private const int PowStartShift = 5;
    private const int PowRangeShift = 8;
    private const int GaugeSpeedShift = 12;

    public static bool TryReadShot(ReadOnlySpan<byte> payload, out GunShotRequest request)
    {
        request = default;
        if (payload.Length < ShotRequestSize) return false;
        var r = new PacketReader(payload);
        request = new GunShotRequest(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16(), r.ReadByte());
        return true;
    }

    public static ushort CheckMask(int powStartPoint, int powRange, int gaugeSpeed) =>
        (ushort)((powStartPoint << PowStartShift) ^ (powRange << PowRangeShift) ^ (gaugeSpeed << GaugeSpeedShift));

    public static bool CheckWordMatches(ushort checkWord, ushort key, int powStartPoint, int powRange, int gaugeSpeed) =>
        ((checkWord ^ CheckMask(powStartPoint, powRange, gaugeSpeed)) & KeyMask) == (key & KeyMask);

    public static byte[] Key(ushort key)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16((ushort)(key & KeyMask));
        return w.ToArray();
    }

    public static byte[] Refused() => new byte[RefusedSize];
}
