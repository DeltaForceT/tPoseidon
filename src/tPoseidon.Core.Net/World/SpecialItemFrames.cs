using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Net.World;

public readonly record struct MegaphoneRequest(ulong WireMicrophoneUid, byte[] Name, byte[] Text);

public readonly record struct MegaphoneShout(string Name, string Text, int ItemIndex, byte ItemClass)
{
    public string Line => Name + Text;
}

public readonly record struct FriendFinderTarget(short X, short Y, ushort Map);

public static class SpecialItemFrames
{
    public const ushort HairChanged = 0x0151;
    public const ushort HairSeen = 0x0152;
    public const ushort PortAlreadyThere = 0x0235;
    public const ushort PortSameZone = 0x0236;
    public const ushort PortNoZone = 0x0237;
    public const ushort GenderSeen = 0x0248;
    public const ushort GenderRefused = 0x0249;
    public const ushort SkillReset = 0x026A;
    public const ushort SkillResetRefused = 0x026C;
    public const ushort StatReset = 0x0275;
    public const ushort StatResetRefused = 0x0277;
    public const ushort StatResetAll = 0x027A;
    public const ushort StatResetAllRefused = 0x027C;
    public const ushort GrowthReset = 0x0289;
    public const ushort GrowthResetRefused = 0x028B;
    public const ushort StorageExpanded = 0x02E6;
    public const ushort StorageRefused = 0x02E7;
    public const ushort Renamed = 0x0314;
    public const ushort RenameRefused = 0x0315;
    public const ushort MegaphoneShout = 0x0321;
    public const ushort MegaphoneSent = 0x0322;
    public const ushort MegaphoneRefused = 0x0323;

    public const int NameFieldBytes = 0x11;
    public const int MegaphoneTextBytes = 0xA1;
    public const int MegaphoneMicrophoneBytes = sizeof(ulong);
    public const uint MegaphoneNoMicrophone = 1;

    public static byte[] FormRefused() => SkillFrames.Notice(SpecialItemEffects.NormalFormRefusalKind, 0, 0);

    public static byte[] Hair(byte color) => [color];

    public static byte[] HairSeenBy(ushort entityId, byte color)
    {
        var w = new PacketWriter(3);
        w.WriteUInt16(entityId);
        w.WriteByte(color);
        return w.ToArray();
    }

    public static byte[] GenderChangedFor(ushort entityId, ushort type, byte job)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(entityId);
        w.WriteUInt16(type);
        w.WriteByte(job);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] SkillResetFor(ushort skillId, ushort refunded)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(skillId);
        w.WriteUInt16(refunded);
        return w.ToArray();
    }

    public static byte[] StatBlock(CharacterStatBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var w = new PacketWriter(CharacterStatBlock.Size);
        block.WriteTo(w);
        return w.ToArray();
    }

    public static byte[] StatResetAllFor(CharacterStatBlock block, uint refunded)
    {
        ArgumentNullException.ThrowIfNull(block);
        var w = new PacketWriter(CharacterStatBlock.Size + sizeof(uint));
        block.WriteTo(w);
        w.WriteUInt32(refunded);
        return w.ToArray();
    }

    public static byte[] GrowthResetFor(ReadOnlySpan<byte> characterInfo, CharacterStatBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        var w = new PacketWriter(characterInfo.Length + CharacterStatBlock.Size);
        w.WriteBytes(characterInfo);
        block.WriteTo(w);
        return w.ToArray();
    }

    public static byte[] StorageExpandedBy(byte slots) => [slots];

    public static byte[] Code(uint code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static byte[] RenamedWith(BagOperation consumed) => QuestResultFrames.ItemOperation(consumed);

    public static byte[] MegaphoneSentWith(BagOperation consumed, uint queued)
    {
        var w = new PacketWriter(28);
        w.WriteBytes(QuestResultFrames.ItemOperation(consumed));
        w.WriteUInt32(queued);
        return w.ToArray();
    }

    public static byte[] MegaphoneShouted(ushort style, string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(3 + text.Length);
        w.WriteUInt16(style);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static bool TryReadName(ReadOnlySpan<byte> data, out byte[] name)
    {
        name = [];
        if (data.Length < 1 || data.Length > NameFieldBytes) return false;
        var end = data.IndexOf((byte)0);
        name = (end < 0 ? data : data[..end]).ToArray();
        return true;
    }

    public static bool TryReadMegaphone(ReadOnlySpan<byte> data, out MegaphoneRequest request)
    {
        request = default;
        if (data.Length < MegaphoneMicrophoneBytes) return false;
        var uid = BinaryPrimitives.ReadUInt64LittleEndian(data);
        var rest = data[MegaphoneMicrophoneBytes..];
        var nameEnd = rest.IndexOf((byte)0);
        if (nameEnd < 0) return false;
        var afterName = rest[(nameEnd + 1)..];
        var textEnd = afterName.IndexOf((byte)0);
        if (textEnd < 0) return false;
        request = new MegaphoneRequest(uid, rest[..nameEnd].ToArray(), afterName[..textEnd].ToArray());
        return true;
    }

    public static bool TryReadWord(ReadOnlySpan<byte> data, out ushort value)
    {
        value = 0;
        if (data.Length < sizeof(ushort)) return false;
        value = BinaryPrimitives.ReadUInt16LittleEndian(data);
        return true;
    }

    public static bool TryReadAdvancedZoneIndex(ReadOnlySpan<byte> data, out uint zoneIndex)
    {
        zoneIndex = 0;
        if (data.Length < sizeof(uint)) return false;
        zoneIndex = BinaryPrimitives.ReadUInt32LittleEndian(data);
        return true;
    }

    public static bool TryReadFriendFinder(ReadOnlySpan<byte> data, out FriendFinderTarget target)
    {
        target = default;
        if (data.Length < 2 * sizeof(short) + sizeof(ushort)) return false;
        target = new FriendFinderTarget(
            BinaryPrimitives.ReadInt16LittleEndian(data),
            BinaryPrimitives.ReadInt16LittleEndian(data[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(data[4..]));
        return true;
    }
}
