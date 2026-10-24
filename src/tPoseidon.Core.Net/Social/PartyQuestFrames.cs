using System.Text;

namespace tPoseidon.Core.Net.Social;

public static class PartyQuestFrames
{
    public const ushort Succeeded = 0x0367;
    public const ushort Rewarded = 0x0369;
    public const ushort MembersBusy = 0x036C;
    public const ushort Cancelled = 0x04A8;
    public const ushort Given = 0x04A9;
    public const ushort Timer = 0x04AA;
    public const ushort Kills = 0x04AB;
    public const ushort TimedOut = 0x04AC;

    public static byte[] GivenFor(uint questId, ushort monsterId, ushort targetCount, uint limitMs, byte completionCount)
    {
        var w = new PacketWriter(13);
        w.WriteUInt32(questId);
        w.WriteUInt16(monsterId);
        w.WriteUInt16(targetCount);
        w.WriteUInt32(limitMs);
        w.WriteByte(completionCount);
        return w.ToArray();
    }

    public static byte[] TimerFor(uint remainingMs, uint limitMs)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32(remainingMs);
        w.WriteUInt32(limitMs);
        return w.ToArray();
    }

    public static byte[] KillsFor(ushort teamKills, ushort targetCount, IReadOnlyList<(ulong CharacterId, ushort Kills)> members)
    {
        ArgumentNullException.ThrowIfNull(members);
        var listed = members.Where(m => m.CharacterId != 0).ToArray();
        var w = new PacketWriter(5 + listed.Length * 10);
        w.WriteUInt16(teamKills);
        w.WriteUInt16(targetCount);
        w.WriteByte((byte)listed.Length);
        foreach (var (characterId, kills) in listed)
        {
            w.WriteUInt64(characterId);
            w.WriteUInt16(kills);
        }
        return w.ToArray();
    }

    public static byte[] CancelledFor(uint questId, uint reason)
    {
        var w = new PacketWriter(8);
        w.WriteUInt32(questId);
        w.WriteUInt32(reason);
        return w.ToArray();
    }

    public static byte[] Quest(uint questId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(questId);
        return w.ToArray();
    }

    public static byte[] SucceededFor(uint questId, bool returnWarp)
    {
        var w = new PacketWriter(5);
        w.WriteUInt32(questId);
        w.WriteByte(returnWarp ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] MembersBusyFor(IReadOnlyList<string> names, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(names);
        var w = new PacketWriter(2 + names.Count * 20);
        w.WriteUInt16((ushort)names.Count);
        foreach (var name in names) w.WriteNulTerminated(name, encoding);
        return w.ToArray();
    }
}
