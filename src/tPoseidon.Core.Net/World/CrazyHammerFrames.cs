using tPoseidon.Core.Game.Skills;

namespace tPoseidon.Core.Net.World;

public static class CrazyHammerFrames
{
    public const int GaugeTailSize = 8;
    public const int GaugeUpdateSize = 6;

    public static byte[] Gauge(ushort entityId, ushort objectId, sbyte effectClass, int current, int max)
    {
        var header = SkillFrames.EffectHeader(
            entityId, entityId, CrazyHammer.GaugeAction, effectClass, CrazyHammer.CrazyHammerSkill, 0, objectId);
        var tail = new PacketWriter(GaugeTailSize);
        tail.WriteInt32(current);
        tail.WriteInt32(max);
        return [.. header, .. tail.ToArray()];
    }

    public static byte[] GaugeUpdate(ushort objectId, int current)
    {
        var w = new PacketWriter(GaugeUpdateSize);
        w.WriteUInt16(objectId);
        w.WriteInt32(current);
        return w.ToArray();
    }
}
