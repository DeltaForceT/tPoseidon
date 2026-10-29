using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.World;

public sealed record ProfileViewInfo(
    ushort CharacterType,
    ushort Level,
    ushort TmLevel,
    byte Job,
    string GuildName,
    ProfileTitle Title,
    ushort Kills,
    ushort Deaths,
    byte[] Message,
    IReadOnlyList<short> GrowthWeights);

public sealed record ProfileViewWorn(byte Slot, GameItemListPacket.ItemRecord Record);

public sealed record ProfileViewEquipment(
    IReadOnlyList<uint> Totals,
    IReadOnlyList<uint> Bonuses,
    IReadOnlyList<ushort> StatLevels,
    IReadOnlyList<byte> Remainders,
    IReadOnlyList<short> Resists,
    IReadOnlyList<ProfileViewWorn> Worn);

public sealed record ProfileView(
    uint Flags,
    string Name,
    ushort EntityId,
    byte FType,
    ProfileViewInfo? Info,
    ProfileViewEquipment? Equipment,
    IReadOnlyList<BossKillHistoryRecord>? Collection = null);

public enum CoverLetterRead
{
    Accepted,
    OutOfRange,
    Truncated,
}

public static class ProfileViewFrames
{
    public const int RequestSize = sizeof(ushort);
    public const int OptionsRequestSize = sizeof(uint);
    public const int CoverLetterHeaderSize = sizeof(short);
    public const int BossKillRecordSize = 12;

    public static bool TryReadRequest(ReadOnlySpan<byte> payload, out ushort entityId)
    {
        entityId = 0;
        if (payload.Length < RequestSize) return false;
        entityId = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static bool TryReadOptions(ReadOnlySpan<byte> payload, out uint flags)
    {
        flags = 0;
        if (payload.Length < OptionsRequestSize) return false;
        flags = new PacketReader(payload).ReadUInt32();
        return true;
    }

    public static CoverLetterRead TryReadCoverLetter(ReadOnlySpan<byte> payload, out byte[] letter)
    {
        letter = [];
        if (payload.Length < CoverLetterHeaderSize) return CoverLetterRead.Truncated;
        var length = new PacketReader(payload).ReadInt16();
        if (length < 0 || length > ProfileViewRules.MaxMessageBytes) return CoverLetterRead.OutOfRange;
        if (payload.Length < CoverLetterHeaderSize + length) return CoverLetterRead.Truncated;
        letter = payload.Slice(CoverLetterHeaderSize, length).ToArray();
        return CoverLetterRead.Accepted;
    }

    public static byte[] CoverLetter(ReadOnlySpan<byte> letter)
    {
        var w = new PacketWriter(CoverLetterHeaderSize + letter.Length);
        w.WriteUInt16((ushort)letter.Length);
        w.WriteBytes(letter);
        return w.ToArray();
    }

    public static byte[] CoverLetterRefused() => [ProfileViewRules.CoverLetterRefusedCode];

    public static byte[] Options(uint flags)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(flags);
        return w.ToArray();
    }

    public static byte[] Unknown() => Options(ProfileViewRules.UnknownPlayerCode);

    public static byte[] View(ProfileView view, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(256);
        w.WriteUInt32(view.Flags);
        w.WriteNulTerminated(view.Name, text);
        w.WriteUInt16(view.EntityId);
        w.WriteByte(view.FType);
        if (!ProfileViewRules.Shows(view.Flags, ProfileViewRules.ShowInfo)) return w.ToArray();
        var info = view.Info ?? throw new InvalidOperationException("The info flag is set but the view carries no info.");
        w.WriteUInt16(info.CharacterType);
        w.WriteUInt16(info.Level);
        w.WriteUInt16(info.TmLevel);
        w.WriteByte(info.Job);
        w.WriteNulTerminated(info.GuildName, text);
        w.WriteUInt16(view.EntityId);
        w.WriteUInt16(info.Title.TitleType);
        w.WriteUInt64(info.Title.TitleId);
        w.WriteUInt16(info.Kills);
        w.WriteUInt16(info.Deaths);
        w.WriteUInt16((ushort)info.Message.Length);
        w.WriteBytes(info.Message);
        RequireCount(info.GrowthWeights.Count, ProfileViewRules.GrowthGroups, "growth weights");
        foreach (var weight in info.GrowthWeights) w.WriteInt16(weight);
        if (ProfileViewRules.Shows(view.Flags, ProfileViewRules.ShowEquipment))
        {
            WriteEquipment(w, view.Equipment ?? throw new InvalidOperationException("The equipment flag is set but the view carries no equipment."), info);
        }
        if (ProfileViewRules.Shows(view.Flags, ProfileViewRules.ShowCollection)) WriteCollection(w, view.Collection ?? []);
        return w.ToArray();
    }

    public static byte[] BossKillWindow(IReadOnlyList<BossKillHistoryRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var shown = records.Take(ProfileViewRules.MostCollectionRecords).ToList();
        var w = new PacketWriter(sizeof(ushort) + shown.Count * BossKillRecordSize);
        w.WriteUInt16((ushort)shown.Count);
        foreach (var record in shown) WriteBossKill(w, record);
        return w.ToArray();
    }

    private static void WriteCollection(PacketWriter w, IReadOnlyList<BossKillHistoryRecord> records)
    {
        var shown = records.Take(ProfileViewRules.MostCollectionRecords).ToList();
        w.WriteByte((byte)shown.Count);
        foreach (var record in shown) WriteBossKill(w, record);
    }

    private static void WriteBossKill(PacketWriter w, BossKillHistoryRecord record)
    {
        w.WriteUInt16((ushort)record.MonsterId);
        w.WriteUInt16((ushort)Math.Clamp(record.KillCount, 0, ushort.MaxValue));
        w.WriteInt64(record.FirstKill.ToFileTime());
    }

    private static void WriteEquipment(PacketWriter w, ProfileViewEquipment equipment, ProfileViewInfo info)
    {
        RequireCount(equipment.Totals.Count, ProfileViewRules.StatCount, "stat totals");
        RequireCount(equipment.Bonuses.Count, ProfileViewRules.StatCount, "stat bonuses");
        RequireCount(equipment.StatLevels.Count, ProfileViewRules.StatCount, "stat levels");
        RequireCount(equipment.Remainders.Count, ProfileViewRules.StatCount, "stat remainders");
        RequireCount(equipment.Resists.Count, ProfileViewRules.ResistSlots, "resists");
        foreach (var total in equipment.Totals) w.WriteUInt32(total);
        foreach (var bonus in equipment.Bonuses) w.WriteUInt32(bonus);
        foreach (var level in equipment.StatLevels) w.WriteUInt16(level);
        foreach (var remainder in equipment.Remainders) w.WriteByte(remainder);
        foreach (var resist in equipment.Resists) w.WriteInt16(resist);
        w.WriteByte((byte)equipment.Worn.Count);
        foreach (var worn in equipment.Worn)
        {
            w.WriteByte(worn.Slot);
            GameItemListPacket.WriteRecord(w, worn.Record);
        }
        w.WriteUInt16(info.CharacterType);
        w.WriteByte(info.Job);
    }

    private static void RequireCount(int count, int expected, string what)
    {
        if (count != expected) throw new InvalidOperationException($"A profile view carries {expected} {what}, got {count}.");
    }
}
