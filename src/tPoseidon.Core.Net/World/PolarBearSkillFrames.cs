namespace tPoseidon.Core.Net.World;

public readonly record struct PolarBearHit(long Damage, int Kind);

public static class PolarBearSkillFrames
{
    public const int DoubleHammer = 1523;
    public const int QuakeHammer = 1530;
    public const int CrazyHammer = 1532;
    public const int CrazyHammerActive = 1533;
    public const int OverSwing = 1535;
    public const int FlyHammer = 1539;
    public const int HighSpeedDrop = 1546;
    public const int HammerAp = 1551;

    public const int DoubleHammerHits = 2;
    public const int OverSwingHits = 3;
    public const int HitEntrySize = 10;
    public const int TargetTailSize = 2;
    public const int GaugeTailSize = 8;
    public const int GaugeChangedSize = 6;
    public const int HitLandsAtMs = 319;
    public const int SwingEndsAtMs = 699;
    public const int FlightMs = 700;

    private const short UnreadEntryField = 0;

    public static int HitsOf(int actionId) => actionId switch
    {
        DoubleHammer => DoubleHammerHits,
        OverSwing => OverSwingHits,
        _ => 0,
    };

    public static byte[] HitsTail(ushort targetEntityId, IReadOnlyList<PolarBearHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        var w = new PacketWriter(TargetTailSize + hits.Count * HitEntrySize);
        w.WriteUInt16(targetEntityId);
        foreach (var hit in hits)
        {
            w.WriteInt32Field(hit.Damage);
            w.WriteInt32(hit.Kind);
            w.WriteInt16(UnreadEntryField);
        }
        return w.ToArray();
    }

    public static byte[] TargetTail(ushort targetEntityId)
    {
        var w = new PacketWriter(TargetTailSize);
        w.WriteUInt16(targetEntityId);
        return w.ToArray();
    }

    public static byte[] GaugeTail(int current, int maximum)
    {
        var w = new PacketWriter(GaugeTailSize);
        w.WriteInt32(current);
        w.WriteInt32(maximum);
        return w.ToArray();
    }

    public static byte[] GaugeChanged(ushort effectObjectId, int current)
    {
        var w = new PacketWriter(GaugeChangedSize);
        w.WriteUInt16(effectObjectId);
        w.WriteInt32(current);
        return w.ToArray();
    }
}
