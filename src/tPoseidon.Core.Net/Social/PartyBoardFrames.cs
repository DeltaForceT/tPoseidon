using System.Text;

namespace tPoseidon.Core.Net.Social;

public readonly record struct PartyBoardList(ushort Page, byte Sort, bool HighToLow, bool NearLevel, ushort Level);

public readonly record struct PartyBoardSearch(ushort Page, byte Sort, bool HighToLow, byte Mode, string Text);

public static class PartyBoardFrames
{
    public const ushort ListParties = 0x1C86;
    public const ushort ListSeekers = 0x1C89;
    public const ushort PostParty = 0x1C8C;
    public const ushort PostSeeker = 0x1C8F;
    public const ushort RemoveParty = 0x1C92;
    public const ushort RemoveSeeker = 0x1C95;
    public const ushort SearchParties = 0x1C98;
    public const ushort SearchSeekers = 0x1C99;
    public const ushort PartyDetailsRequest = 0x1C9A;
    public const ushort SeekerDetailsRequest = 0x1C9D;

    public const ushort Refused = 0x1C85;
    public const ushort PartyList = 0x1C87;
    public const ushort SeekerList = 0x1C8A;
    public const ushort PartyPosted = 0x1C8D;
    public const ushort SeekerPosted = 0x1C90;
    public const ushort PartyRemoved = 0x1C93;
    public const ushort PartyRemoveWithoutParty = 0x1C94;
    public const ushort SeekerRemoved = 0x1C96;
    public const ushort PartyDetails = 0x1C9B;
    public const ushort PartyDetailsMissing = 0x1C9C;
    public const ushort SeekerDetails = 0x1C9E;
    public const ushort SeekerDetailsMissing = 0x1C9F;

    public const uint PartyFull = 9;
    public const uint AlreadyInParty = 0x0D;
    public const uint MasterOnly = 0x0E;
    public const uint AlreadyPosted = 0x0F;
    public const uint BoardFull = 0x10;
    public const uint NotInParty = 0x11;

    public const byte SortDefault = 0;
    public const byte PartySortByType = 1;
    public const byte PartySortByLevel = 4;
    public const byte SeekerSortDefaultAlias = 7;
    public const byte SeekerSortByLevel = 8;

    public const byte SearchByName = 0;
    public const byte SearchLevelAtLeast = 1;
    public const byte SearchLevelAtMost = 2;

    public const int PageSize = 8;
    public const int RecordLength = 0x48;
    public const int NameLength = 20;
    public const int CommentLength = 35;
    public const int PartyCapacity = 1000;
    public const int SeekerCapacity = 1500;
    public const int LevelWindow = 3;
    public const int FilteredCapacity = 24;

    public const ushort OrderHeadNotice = 0xD4;
    public const byte HeadNoticeForParty = 1;
    public const byte HeadNoticeForSeeker = 2;
    public const int HeadNoticeDataLength = 40;

    public const ushort HeadNoticeShown = 0x01D6;
    public const ushort HeadNoticeCleared = 0x01D7;

    public static bool TryReadList(ReadOnlySpan<byte> body, bool withLevelFilter, out PartyBoardList list)
    {
        list = default;
        var r = new PacketReader(body);
        if (r.Remaining < (withLevelFilter ? 7 : 4)) return false;
        var page = r.ReadUInt16();
        var sort = r.ReadByte();
        var highToLow = r.ReadByte() != 0;
        if (!withLevelFilter)
        {
            list = new PartyBoardList(page, sort, highToLow, false, 0);
            return true;
        }
        var nearLevel = r.ReadByte() != 0;
        list = new PartyBoardList(page, sort, highToLow, nearLevel, r.ReadUInt16());
        return true;
    }

    public static bool TryReadSearch(ReadOnlySpan<byte> body, Encoding text, out PartyBoardSearch search)
    {
        search = default;
        var r = new PacketReader(body);
        if (r.Remaining < 6) return false;
        var page = r.ReadUInt16();
        var sort = r.ReadByte();
        var highToLow = r.ReadByte() != 0;
        var mode = r.ReadByte();
        search = new PartyBoardSearch(page, sort, highToLow, mode, r.ReadNulTerminated(text));
        return true;
    }

    public static byte[] RefusedFor(uint code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static byte[] PageOf(IReadOnlyList<byte[]> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var w = new PacketWriter(1 + records.Count * RecordLength);
        w.WriteByte((byte)records.Count);
        foreach (var record in records) w.WriteBytes(record);
        return w.ToArray();
    }

    public static byte[] PartyRecord(
        ushort partyId, uint masterType, uint partyType, ushort averageLevel, string masterName, string comment, Encoding text)
    {
        var w = new PacketWriter(RecordLength);
        w.WriteUInt16(partyId);
        w.WriteUInt16(0);
        w.WriteUInt32(masterType);
        w.WriteUInt32(partyType);
        w.WriteUInt16(averageLevel);
        WriteField(w, masterName, NameLength, text);
        WriteField(w, comment, CommentLength, text);
        w.WriteZero(RecordLength - w.Position);
        return w.ToArray();
    }

    public static byte[] SeekerRecord(ulong characterId, uint type, ushort level, string name, string comment, Encoding text)
    {
        var w = new PacketWriter(RecordLength);
        w.WriteUInt64(characterId);
        w.WriteUInt32(type);
        w.WriteUInt16(level);
        WriteField(w, name, NameLength, text);
        WriteField(w, comment, CommentLength, text);
        w.WriteZero(RecordLength - w.Position);
        return w.ToArray();
    }

    public static byte[] PartyDetailsFor(
        string masterName, ushort masterLevel, uint masterZone, byte memberCount, byte divisionType, byte itemShare,
        string comment, Encoding text)
    {
        var w = new PacketWriter(64);
        w.WriteNulTerminated(masterName, text);
        w.WriteUInt16(masterLevel);
        w.WriteUInt32(masterZone);
        w.WriteByte(memberCount);
        w.WriteByte(divisionType);
        w.WriteByte(itemShare);
        w.WriteNulTerminated(comment, text);
        return w.ToArray();
    }

    public static byte[] SeekerDetailsFor(string name, uint zone, string comment, Encoding text)
    {
        var w = new PacketWriter(64);
        w.WriteNulTerminated(name, text);
        w.WriteUInt32(zone);
        w.WriteNulTerminated(comment, text);
        return w.ToArray();
    }

    public static byte[] HeadNoticeOrderData(string comment, byte kind, bool active, Encoding text)
    {
        var w = new PacketWriter(HeadNoticeDataLength);
        WriteField(w, comment, CommentLength, text);
        w.WriteUInt32(kind);
        w.WriteByte(active ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static bool TryReadHeadNoticeOrder(ReadOnlySpan<byte> data, Encoding text, out string comment, out byte kind, out bool active)
    {
        comment = string.Empty;
        kind = 0;
        active = false;
        if (data.Length < HeadNoticeDataLength) return false;
        var field = data[..CommentLength];
        var end = field.IndexOf((byte)0);
        comment = text.GetString(end < 0 ? field : field[..end]);
        kind = data[CommentLength];
        active = data[CommentLength + sizeof(uint)] != 0;
        return true;
    }

    public static byte[] HeadNoticeShownFor(ushort entityId, byte kind, string comment, Encoding text)
    {
        var w = new PacketWriter(sizeof(ushort) + 1 + 51);
        w.WriteUInt16(entityId);
        w.WriteByte(kind);
        w.WriteBytes(Truncated(comment, 50, text));
        w.WriteByte(0);
        return w.ToArray();
    }

    public static ushort ParseNumber(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index])) index++;
        var negative = false;
        if (index < text.Length && text[index] is '+' or '-')
        {
            negative = text[index] == '-';
            index++;
        }
        long value = 0;
        while (index < text.Length && text[index] is >= '0' and <= '9' && value <= int.MaxValue)
        {
            value = value * 10 + (text[index] - '0');
            index++;
        }
        return unchecked((ushort)(negative ? -value : value));
    }

    private static void WriteField(PacketWriter w, string value, int slot, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(value);
        var kept = Truncated(value, slot - 1, text);
        w.WriteBytes(kept);
        w.WriteZero(slot - kept.Length);
    }

    private static byte[] Truncated(string value, int limit, Encoding text)
    {
        var bytes = text.GetBytes(value);
        return bytes.Length <= limit ? bytes : bytes[..limit];
    }
}
