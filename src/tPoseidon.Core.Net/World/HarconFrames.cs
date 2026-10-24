using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.HarconDefence;

namespace tPoseidon.Core.Net.World;

public readonly record struct HarconCreateRequest(ushort StageId, string Title, byte MaxPlayers);

public readonly record struct HarconShopRequest(ushort NpcType, uint ItemId);

public static class HarconFrames
{
    public const ushort ShopOpened = 0x05B6;
    public const ushort ShopClose = 0x05B7;
    public const ushort ShopClosed = 0x05B8;
    public const int RoomRecordSize = 0x69;
    public const int RoomTitleSize = 0x40;
    public const int RoomOwnerSize = 0x20;
    public const int MemberRecordSize = 0x23;
    public const int MemberNameSize = 0x20;
    public const int NoChangeBodySize = 14;
    public const int TotalRecordSize = 0x3A;
    public const int TotalRecordStagesEnd = 0x1E;
    public const int StageRecordSize = 0x35;
    private const int StageRecordUnusedCounts = 3;
    private const int StageRecordBattleCounts = 8;

    public static byte[] Lobby(byte category, int coins, IReadOnlyCollection<int> cleared)
    {
        ArgumentNullException.ThrowIfNull(cleared);
        var w = new PacketWriter(8 + cleared.Count * 2);
        w.WriteByte(category);
        w.WriteUInt32((uint)Math.Max(0, coins));
        w.WriteByte((byte)Math.Min(cleared.Count, byte.MaxValue));
        foreach (var stage in cleared.Take(byte.MaxValue)) w.WriteUInt16((ushort)stage);
        return w.ToArray();
    }

    public static byte[] RoomList(IReadOnlyCollection<HarconRoom> rooms, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(rooms);
        var shown = rooms.Take(byte.MaxValue).ToArray();
        var w = new PacketWriter(1 + shown.Length * RoomRecordSize);
        w.WriteByte((byte)shown.Length);
        foreach (var room in shown) WriteRoom(w, room, text);
        return w.ToArray();
    }

    public static byte[] Room(HarconRoom room, Encoding text)
    {
        var w = new PacketWriter(RoomRecordSize);
        WriteRoom(w, room, text);
        return w.ToArray();
    }

    public static byte[] Created(HarconRoom room, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(room);
        var w = new PacketWriter(16 + room.Title.Length * 2);
        w.WriteUInt32(room.Key);
        w.WriteUInt16((ushort)room.Stage.Id);
        w.WriteNulTerminated(room.Title, text);
        return w.ToArray();
    }

    public static byte[] Member(HarconMember member, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(member);
        var w = new PacketWriter(MemberRecordSize);
        WriteFixed(w, member.Name, MemberNameSize, text);
        w.WriteUInt16(member.EntityId);
        w.WriteByte(member.ReadyState);
        return w.ToArray();
    }

    public static byte[] RoomValue(uint key, byte value)
    {
        var w = new PacketWriter(5);
        w.WriteUInt32(key);
        w.WriteByte(value);
        return w.ToArray();
    }

    public static byte[] RoomKey(uint key)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(key);
        return w.ToArray();
    }

    public static byte[] Name(string name, Encoding text)
    {
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] ReadyAnswer(string name, byte answer, Encoding text)
    {
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, text);
        w.WriteByte(answer);
        return w.ToArray();
    }

    public const int MaxGauges = 6;

    public static byte[] GameStart(IReadOnlyList<HarconMember> entered, int failCount, IReadOnlyList<(ushort EntityId, long Value)> gauges, byte[] itemChange, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(entered);
        ArgumentNullException.ThrowIfNull(gauges);
        ArgumentNullException.ThrowIfNull(itemChange);
        var w = new PacketWriter(64);
        w.WriteByte((byte)Math.Clamp(failCount, 0, byte.MaxValue));
        w.WriteByte((byte)entered.Count);
        foreach (var member in entered)
        {
            w.WriteUInt16(member.EntityId);
            w.WriteNulTerminated(member.Name, text);
        }
        WriteGauges(w, gauges);
        w.WriteBytes(itemChange);
        return w.ToArray();
    }

    public static byte[] Gauges(IReadOnlyList<(ushort EntityId, long Value)> gauges)
    {
        ArgumentNullException.ThrowIfNull(gauges);
        var w = new PacketWriter(1 + gauges.Count * 6);
        WriteGauges(w, gauges);
        return w.ToArray();
    }

    private static void WriteGauges(PacketWriter w, IReadOnlyList<(ushort EntityId, long Value)> gauges)
    {
        var count = Math.Min(gauges.Count, MaxGauges);
        w.WriteByte((byte)count);
        for (var index = 0; index < count; index++)
        {
            w.WriteUInt16(gauges[index].EntityId);
            w.WriteUInt32Field(gauges[index].Value);
        }
    }

    public static byte[] NoItemChange()
    {
        var w = new PacketWriter(2 + NoChangeBodySize);
        w.WriteByte(0);
        w.WriteByte(0);
        w.WriteZero(NoChangeBodySize);
        return w.ToArray();
    }

    public static byte[] TotalRecord(HarconDefTotals totals)
    {
        ArgumentNullException.ThrowIfNull(totals);
        var w = new PacketWriter(TotalRecordSize);
        w.WriteUInt32((uint)Math.Max(0, totals.Plays));
        w.WriteUInt16((ushort)Math.Clamp(totals.Wins, 0, ushort.MaxValue));
        w.WriteUInt16((ushort)Math.Clamp(totals.Losses, 0, ushort.MaxValue));
        w.WriteUInt16(0);
        w.WriteUInt32(0);
        w.WriteUInt32(0);
        for (var slot = 0; slot < HarconDefTotals.ShownStages; slot++)
        {
            var (stageId, clears) = slot < totals.TopStages.Count ? totals.TopStages[slot] : (0, 0);
            w.WriteUInt16((ushort)Math.Clamp(stageId, 0, ushort.MaxValue));
            w.WriteUInt16((ushort)Math.Clamp(clears, 0, ushort.MaxValue));
        }
        w.WriteZero(TotalRecordSize - TotalRecordStagesEnd);
        return w.ToArray();
    }

    public static byte[] StageRecord(HarconStageRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var w = new PacketWriter(2 + StageRecordSize);
        w.WriteUInt16((ushort)Math.Clamp(record.StageId, 0, ushort.MaxValue));
        w.WriteUInt16(Word(record.Plays));
        w.WriteUInt16(Word(record.Wins));
        w.WriteUInt16(Word(record.Losses));
        for (var unused = 0; unused < StageRecordUnusedCounts; unused++) w.WriteUInt16(0);
        w.WriteUInt32((uint)Math.Clamp(record.TotalScore, 0, uint.MaxValue));
        w.WriteUInt32(BitConverter.SingleToUInt32Bits(record.AverageContribution));
        w.WriteUInt32(0);
        w.WriteByte((byte)Math.Clamp(record.BestRank, 0, byte.MaxValue));
        w.WriteUInt32(BitConverter.SingleToUInt32Bits(record.BestContribution));
        w.WriteUInt16(Word(record.BestDeaths));
        for (var count = 0; count < StageRecordBattleCounts; count++) w.WriteUInt16(0);
        w.WriteUInt32((uint)Math.Max(0, record.BestTowerScore));
        w.WriteUInt16(Word(record.BestTowersBuilt));
        return w.ToArray();
    }

    public static bool TryReadStage(ReadOnlySpan<byte> payload, out ushort stageId)
    {
        stageId = payload.Length >= 2 ? new PacketReader(payload).ReadUInt16() : (ushort)0;
        return payload.Length >= 2;
    }

    public static bool TryReadRoomKey(ReadOnlySpan<byte> payload, out uint key)
    {
        key = payload.Length >= 4 ? new PacketReader(payload).ReadUInt32() : 0;
        return payload.Length >= 4;
    }

    public static bool TryReadName(ReadOnlySpan<byte> payload, Encoding text, out string name)
    {
        name = string.Empty;
        try
        {
            name = new PacketReader(payload).ReadNulTerminated(text);
            return name.Length > 0;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static byte[] GameEnd(bool cleared, int coins)
    {
        var w = new PacketWriter(5);
        w.WriteByte(cleared ? (byte)1 : (byte)0);
        w.WriteUInt32((uint)Math.Max(0, coins));
        return w.ToArray();
    }

    public static byte[] Bought(byte[] itemChange, int coins)
    {
        ArgumentNullException.ThrowIfNull(itemChange);
        var w = new PacketWriter(itemChange.Length + 4);
        w.WriteBytes(itemChange);
        w.WriteUInt32((uint)Math.Max(0, coins));
        return w.ToArray();
    }

    public static bool TryReadCreate(ReadOnlySpan<byte> payload, Encoding text, out HarconCreateRequest request)
    {
        request = default;
        try
        {
            var r = new PacketReader(payload);
            var stage = r.ReadUInt16();
            var title = r.ReadNulTerminated(text);
            var max = r.ReadByte();
            request = new HarconCreateRequest(stage, title, max);
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static byte[] ShopOpen(ushort npcType)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(npcType);
        return w.ToArray();
    }

    public static bool TryReadShop(ReadOnlySpan<byte> payload, out HarconShopRequest request)
    {
        request = default;
        if (payload.Length < 6) return false;
        var r = new PacketReader(payload);
        request = new HarconShopRequest(r.ReadUInt16(), r.ReadUInt32());
        return true;
    }

    private static void WriteRoom(PacketWriter w, HarconRoom room, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(room);
        w.WriteUInt32(room.Key);
        w.WriteUInt16((ushort)room.Stage.Id);
        WriteFixed(w, room.Title, RoomTitleSize, text);
        WriteFixed(w, room.Master?.Name ?? string.Empty, RoomOwnerSize, text);
        w.WriteByte((byte)room.Members.Count);
        w.WriteByte(room.MaxPlayers);
        w.WriteByte(room.InProgress ? (byte)1 : (byte)0);
    }

    private static void WriteFixed(PacketWriter w, string value, int size, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var bytes = text.GetBytes(value ?? string.Empty);
        var length = Math.Min(bytes.Length, size - 1);
        w.WriteBytes(bytes.AsSpan(0, length));
        w.WriteZero(size - length);
    }

    private static ushort Word(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);
}
