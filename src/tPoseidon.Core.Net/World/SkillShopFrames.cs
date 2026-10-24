using System.Text;

namespace tPoseidon.Core.Net.World;

public static class SkillShopFrames
{
    public const ushort JobChangeRequest = 0x0239;
    public const ushort JobChanged = 0x023A;
    public const ushort JobChangeRefused = 0x023B;
    public const ushort AppearanceChanged = 0x0246;
    public const int JobChangeRequestSize = 2;

    public static byte[] JobChangedFor(ushort entityId, ushort type, byte job, byte hair, byte tier)
    {
        var w = new tPoseidon.Core.Net.PacketWriter(8);
        w.WriteUInt16(entityId);
        w.WriteUInt16(type);
        w.WriteByte(job);
        w.WriteByte(hair);
        w.WriteByte(tier);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] BareAppearance(ushort entityId)
    {
        var w = new tPoseidon.Core.Net.PacketWriter(10);
        w.WriteUInt16(entityId);
        w.WriteUInt32(0);
        w.WriteUInt32(0);
        return w.ToArray();
    }

    public const int OpenRequestSize = 2;
    public const int TalkRequestSize = 3;
    public const int ScriptHeaderSize = 4;
    public const int MasterRequestSize = 4;

    public static byte[] Opened(ushort npcEntityId, ushort npcType, int pageCount, string firstPage, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(firstPage);
        ArgumentNullException.ThrowIfNull(textEncoding);
        var count = (byte)Math.Clamp(pageCount, 0, byte.MaxValue);
        var text = count > 0 ? textEncoding.GetBytes(firstPage) : Array.Empty<byte>();
        var w = new PacketWriter(6 + text.Length);
        w.WriteUInt16(npcEntityId);
        w.WriteUInt16(npcType);
        w.WriteByte(count);
        if (count > 0)
        {
            w.WriteBytes(text);
            w.WriteByte(0);
        }
        return w.ToArray();
    }

    public static byte[] Page(ushort npcEntityId, string text, ushort kind, ushort jobQuestId, ushort index, Encoding textEncoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(textEncoding);
        var bytes = textEncoding.GetBytes(text);
        var w = new PacketWriter(9 + bytes.Length);
        w.WriteUInt16(npcEntityId);
        w.WriteBytes(bytes);
        w.WriteByte(0);
        w.WriteUInt16(kind);
        w.WriteUInt16(jobQuestId);
        w.WriteUInt16(index);
        return w.ToArray();
    }

    public static byte[] NpcOnly(ushort npcEntityId)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(npcEntityId);
        return w.ToArray();
    }

    public static byte[] MasterList(IReadOnlyList<int> skillIds)
    {
        ArgumentNullException.ThrowIfNull(skillIds);
        var w = new PacketWriter(2 + 2 * skillIds.Count);
        w.WriteUInt16((ushort)skillIds.Count);
        foreach (var skillId in skillIds) w.WriteUInt16((ushort)skillId);
        return w.ToArray();
    }

    public static byte[] JobChangeView(ushort changeType, ushort changeJob)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(changeType);
        w.WriteUInt16(changeJob);
        return w.ToArray();
    }
}
