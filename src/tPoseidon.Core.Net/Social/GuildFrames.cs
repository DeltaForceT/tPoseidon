using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Social;

public sealed record GuildBlock(ulong CharacterId, uint GuildCode, string GuildName, string MasterName, string Greeting);

public sealed record GuildStateView(GuildBlock Block, ushort MaxMembers, byte GradeIndex, ushort Rights, IReadOnlyList<string> GradeNames);

public sealed record GuildMemberRow(
    ulong CharacterId,
    byte GradeIndex,
    byte Job,
    ushort Level,
    int MemberPoint,
    DateTimeOffset JoinedAt,
    string Name,
    DateTimeOffset? LastLogoutAt);

public sealed record GuildVolunteerRow(ulong CharacterId, ushort Level, string Name, uint Job, DateTimeOffset AppliedAt);

public sealed record GuildDetailView(uint GuildCode, string GuildName, string MasterName, int GuildPoint, int MemberCount, string Intro);

public sealed record GuildCreateRequest(string GuildName, string Intro);

public static class GuildFrames
{
    public const ushort SessionOpen = (ushort)Opcode.HANDSHAKE_STEP_5;
    public const ushort Create = (ushort)Opcode.C2S_GUILD_CREATE;
    public const ushort Apply = (ushort)Opcode.C2S_GUILD_APPLY;
    public const ushort Leave = (ushort)Opcode.C2S_GUILD_LEAVE;
    public const ushort CancelApply = (ushort)Opcode.C2S_GUILD_CANCEL_APPLY;
    public const ushort MemberList = (ushort)Opcode.C2S_GUILD_MEMBER_LIST;
    public const ushort VolunteerList = (ushort)Opcode.C2S_GUILD_VOLUNTEER_LIST;
    public const ushort ForceOut = (ushort)Opcode.C2S_GUILD_FORCE_OUT;
    public const ushort AnswerApply = (ushort)Opcode.C2S_GUILD_ANSWER_APPLY;
    public const ushort Dissolve = (ushort)Opcode.C2S_GUILD_DISSOLVE;
    public const ushort OwnInfo = (ushort)Opcode.CHAR_SELECT;
    public const ushort DetailByCode = (ushort)Opcode.C2S_GUILD_DETAIL_BY_CODE;
    public const ushort DetailByCharacter = (ushort)Opcode.C2S_GUILD_DETAIL_BY_CHARACTER;

    public const ushort Created = (ushort)Opcode.S2C_GUILD_CREATED;
    public const ushort ApplyResult = (ushort)Opcode.S2C_GUILD_APPLY_RESULT;
    public const ushort NewVolunteer = (ushort)Opcode.S2C_GUILD_NEW_VOLUNTEER;
    public const ushort LeaveResult = (ushort)Opcode.S2C_GUILD_LEAVE_RESULT;
    public const ushort MasterAnswered = (ushort)Opcode.S2C_GUILD_MASTER_ANSWERED;
    public const ushort CancelApplyResult = (ushort)Opcode.S2C_GUILD_CANCEL_APPLY_RESULT;
    public const ushort MemberListResult = (ushort)Opcode.S2C_GUILD_MEMBER_LIST;
    public const ushort MembersRemoved = (ushort)Opcode.S2C_GUILD_MEMBERS_REMOVED;
    public const ushort MemberJoined = (ushort)Opcode.S2C_GUILD_MEMBER_JOINED;
    public const ushort VolunteerListResult = (ushort)Opcode.S2C_GUILD_VOLUNTEER_LIST;
    public const ushort ForceOutResult = (ushort)Opcode.S2C_GUILD_FORCE_OUT_RESULT;
    public const ushort AnswerApplyResult = (ushort)Opcode.S2C_GUILD_ANSWER_APPLY_RESULT;
    public const ushort DissolveResult = (ushort)Opcode.S2C_GUILD_DISSOLVE_RESULT;
    public const ushort OwnInfoResult = (ushort)Opcode.CHAR_DATA;
    public const ushort State = (ushort)Opcode.S2C_GUILD_STATE;
    public const ushort Detail = (ushort)Opcode.S2C_GUILD_DETAIL;
    public const ushort NameTag = (ushort)Opcode.S2C_GUILD_NAME_TAG;

    public const int RequestHeaderLength = 12;
    public const int OwnInfoRequestLength = 8;
    public const int CreateRequestLength = 0x9A;
    public const int ApplyRequestLength = 0x2C;
    public const int MemberListRequestLength = 0x10;
    public const int ForceOutHeaderLength = 0x0D;
    public const int ForceOutEntryLength = 0x18;
    public const int AnswerApplyRequestLength = 0x18;
    public const int DetailByCodeRequestLength = 4;
    public const int DetailByCharacterRequestLength = 0x14;

    public const int OwnInfoLength = 0x37;
    public const int StateLength = 0x308;
    public const int CreatedLength = 0xCB;
    public const int ApplyResultLength = 0x18;
    public const int NewVolunteerLength = 0x30;
    public const int LeaveResultLength = 0x22;
    public const int MasterAnsweredLength = 0x1A;
    public const int MemberListHeaderLength = 0x10;
    public const int MemberEntryLength = 0x68;
    public const int RemovedEntryLength = 0x28;
    public const int VolunteerHeaderLength = 4;
    public const int ForceOutResultHeaderLength = 0x0E;
    public const int AnswerApplyResultLength = 0x14;
    public const int DissolveResultLength = 2;
    public const int DetailLength = 0x110;
    public const int NameOrderLength = 0x2E;
    public const int BlockLength = 0x80;
    public const int GradeCount = 7;
    public const int GradeRightsStride = 8;
    public const int GradeTableLength = 0x6C;
    public const int GradeTableHeadLength = 0x1C;
    public const int GradeNameLength = 11;
    public const int NoticeSlots = 3;
    public const int NoticeEntryLength = 0x90;

    public const int GuildNameLength = 25;
    public const int BlockGuildNameLength = 24;
    public const int AliasLength = 21;
    public const int CharacterNameLength = 20;
    public const int IntroLength = 101;
    public const int GreetingLength = 0x3D;
    public const int DateLength = 9;
    public const int DetailMasterNameLength = 0x23;
    public const int DetailIntroLength = 0x43;

    public const byte Ok = 0;
    public const byte Failed = 1;
    public const byte InGuildFlag = 1;

    public const byte OwnInfoQueryFail = 0;
    public const byte OwnInfoNotMyCharacter = 1;
    public const byte OwnInfoApplying = 3;

    public const byte CreateExistingName = 1;
    public const byte CreateOneMasterPerAccount = 2;
    public const byte CreatePenalty = 3;
    public const byte CreateCandidate = 4;
    public const byte CreateAlreadyMember = 5;
    public const byte CreateError = 6;

    public const byte ApplyOtherGuildMember = 1;
    public const byte ApplyOnApplicant = 2;
    public const byte ApplyPenalty = 3;
    public const byte ApplyTargetFull = 4;
    public const byte ApplyBlockedByGm = 5;
    public const byte ApplyError = 6;

    public const byte CancelNotApplying = 1;
    public const byte CancelAlreadyMember = 2;
    public const byte CancelError = 3;

    public const byte LeaveInvalidGuild = 0;
    public const byte LeaveMaster = 1;
    public const byte LeaveNotInGuild = 2;

    public const uint MemberListNotMember = 0;

    public const byte VolunteerQueryFail = 1;
    public const byte VolunteerNoPermission = 2;

    public const byte ForceOutInvalidGuild = 1;
    public const byte ForceOutOnlyMaster = 2;
    public const byte ForceOutPreviousRequest = 3;
    public const byte ForceOutNoPermission = 4;
    public const uint ForceOutRemoved = 0;
    public const uint ForceOutSkipped = 1;

    public const uint AnswerInvalidGuild = 0;
    public const uint AnswerAlreadyMember = 1;
    public const uint AnswerInvalidVolunteer = 2;
    public const uint AnswerOnlyForMaster = 3;
    public const uint AnswerMemberFull = 4;
    public const uint AnswerQueryFail = 5;
    public const uint AnswerNoRight = 6;
    public const uint AnswerAlreadyOurMember = 7;
    public const uint AnswerAccept = 0;

    public const byte MasterAnswerOk = 0;
    public const byte MasterAnswerCancel = 1;

    public const byte DissolveInvalidGuild = 0;
    public const byte DissolveNotMaster = 1;
    public const byte DissolveError = 2;

    public const byte DetailNoCharacter = 1;
    public const byte DetailNotFound = 2;

    public const ushort RightEditMembers = 0x01;
    public const ushort RightVolunteers = 0x02;
    public const ushort MasterRights = 0xFFFF;
    public const int MostMembersToAccept = 0x7E;

    private const int ForceOutEntryUidOffset = 8;
    private const int ForceOutEntryStatusOffset = 0x10;

    public static string ShortDate(DateTimeOffset at) => at.ToString("yy-MM-dd", CultureInfo.InvariantCulture);

    public static byte[] OwnInfoFor(uint guildCode, ulong characterId, string alias, byte gradeIndex, bool newNotice, bool inGuild, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(alias);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(OwnInfoLength);
        w.WriteByte(Ok);
        w.WriteByte(0);
        w.WriteByte(newNotice ? (byte)1 : (byte)0);
        w.WriteByte(inGuild ? InGuildFlag : (byte)0);
        w.WriteByte(inGuild ? InGuildFlag : (byte)0);
        w.WriteZero(2);
        w.WriteUInt32(guildCode);
        w.WriteUInt64(characterId);
        WriteText(w, alias, AliasLength, text);
        w.WriteZero(3);
        w.WriteUInt32(gradeIndex);
        w.WriteZero(OwnInfoLength - 0x2F);
        return w.ToArray();
    }

    public static byte[] OwnInfoFailed(byte reason)
    {
        var payload = new byte[OwnInfoLength];
        payload[0] = Failed;
        payload[1] = reason;
        return payload;
    }

    public static byte[] StateFor(GuildStateView state, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(StateLength);
        w.WriteZero(0x11);
        w.WriteUInt16(state.MaxMembers);
        w.WriteZero(0x23 - 0x13);
        w.WriteUInt32(0);
        w.WriteUInt32(Ok);
        WriteBlock(w, state.Block, text);
        w.WriteZero(8);
        WriteGradeRights(w, state.GradeIndex, state.Rights);
        WriteGradeTable(w, state.GradeNames, text);
        w.WriteByte(0);
        w.WriteZero(NoticeSlots * NoticeEntryLength);
        return w.ToArray();
    }

    public static byte[] CreatedFor(GuildBlock block, byte gradeIndex, ushort rights, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(block);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(CreatedLength);
        w.WriteByte(Ok);
        w.WriteZero(0x0B - 1);
        WriteBlock(w, block, text);
        w.WriteZero(8);
        WriteGradeRights(w, gradeIndex, rights);
        return w.ToArray();
    }

    public static byte[] CreateRefused(byte code, byte penaltyType, string releaseDate, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(releaseDate);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(CreatedLength);
        w.WriteByte(code);
        w.WriteByte(penaltyType);
        WriteText(w, releaseDate, DateLength, text);
        w.WriteZero(CreatedLength - 2 - DateLength);
        return w.ToArray();
    }

    public static byte[] ApplyResultFor(byte code, bool someoneToAnswer, byte penaltyType, string releaseDate, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(releaseDate);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(ApplyResultLength);
        w.WriteByte(code);
        w.WriteByte(someoneToAnswer ? (byte)1 : (byte)0);
        w.WriteZero(0x0E - 2);
        w.WriteByte(penaltyType);
        WriteText(w, releaseDate, DateLength, text);
        return w.ToArray();
    }

    public static byte[] NewVolunteerNotice() => new byte[NewVolunteerLength];

    public static byte[] LeaveResultFor(ulong characterId, string name, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(LeaveResultLength);
        w.WriteZero(4);
        w.WriteUInt64(characterId);
        WriteText(w, name, CharacterNameLength, text);
        w.WriteByte(0);
        w.WriteByte(Ok);
        return w.ToArray();
    }

    public static byte[] LeaveRefused(byte reason)
    {
        var payload = new byte[LeaveResultLength];
        payload[0x20] = reason;
        payload[0x21] = Failed;
        return payload;
    }

    public static byte[] MasterAnsweredFor(string guildName, byte answer, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(MasterAnsweredLength);
        WriteText(w, guildName, GuildNameLength, text);
        w.WriteByte(answer);
        return w.ToArray();
    }

    public static byte[] CancelApplyResultFor(byte code) => new[] { code };

    public static byte[] MemberListFor(IReadOnlyList<GuildMemberRow> members, uint mode, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(MemberListHeaderLength + members.Count * MemberEntryLength);
        w.WriteUInt32(Ok);
        w.WriteUInt32(0);
        w.WriteUInt32((uint)members.Count);
        w.WriteUInt32(mode);
        foreach (var member in members)
        {
            w.WriteUInt64(member.CharacterId);
            w.WriteByte(member.GradeIndex);
            w.WriteByte(member.Job);
            w.WriteUInt16(member.Level);
            w.WriteInt32(member.MemberPoint);
            w.WriteZero(0x24 - 0x10);
            w.WriteByte(0);
            WriteText(w, ShortDate(member.JoinedAt), DateLength, text);
            WriteText(w, member.Name, CharacterNameLength, text);
            WriteText(w, member.LastLogoutAt is { } loggedOut ? ShortDate(loggedOut) : string.Empty, DateLength, text);
            w.WriteZero(MemberEntryLength - 0x4B);
        }
        return w.ToArray();
    }

    public static byte[] MemberListRefused(uint mode)
    {
        var w = new PacketWriter(MemberListHeaderLength);
        w.WriteUInt32(Failed);
        w.WriteUInt32(MemberListNotMember);
        w.WriteUInt32(0);
        w.WriteUInt32(mode);
        return w.ToArray();
    }

    public static byte[] MembersRemovedFor(IReadOnlyList<ulong> characterIds)
    {
        ArgumentNullException.ThrowIfNull(characterIds);
        var w = new PacketWriter(4 + characterIds.Count * RemovedEntryLength);
        w.WriteUInt32((uint)characterIds.Count);
        foreach (var characterId in characterIds)
        {
            w.WriteUInt64(characterId);
            w.WriteZero(RemovedEntryLength - 8);
        }
        return w.ToArray();
    }

    public static byte[] MemberJoinedFor(ulong characterId, string name, ushort level, byte job, string joinedDate, string lastLoginDate, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(joinedDate);
        ArgumentNullException.ThrowIfNull(lastLoginDate);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteUInt64(characterId);
        w.WriteNulTerminated(name, text);
        w.WriteUInt16(level);
        w.WriteByte(job);
        w.WriteNulTerminated(joinedDate, text);
        w.WriteNulTerminated(lastLoginDate, text);
        return w.ToArray();
    }

    public static byte[] VolunteerListFor(IReadOnlyList<GuildVolunteerRow> volunteers, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(volunteers);
        ArgumentNullException.ThrowIfNull(text);
        if (volunteers.Count > byte.MaxValue) throw new ArgumentException($"An applicant list holds at most {byte.MaxValue} entries.", nameof(volunteers));
        var w = new PacketWriter(VolunteerHeaderLength + volunteers.Count * 48);
        w.WriteByte(Ok);
        w.WriteByte(0);
        w.WriteByte((byte)volunteers.Count);
        w.WriteByte((byte)volunteers.Count);
        foreach (var volunteer in volunteers)
        {
            w.WriteUInt64(volunteer.CharacterId);
            w.WriteUInt16(volunteer.Level);
            w.WriteNulTerminated(volunteer.Name, text);
            w.WriteUInt32(volunteer.Job);
            w.WriteNulTerminated(ShortDate(volunteer.AppliedAt), text);
        }
        return w.ToArray();
    }

    public static byte[] VolunteerListRefused(byte reason) => new[] { Failed, reason, (byte)0, (byte)0 };

    public static byte[] ForceOutResultFor(byte code, IReadOnlyList<(byte[] Entry, uint Status)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var w = new PacketWriter(ForceOutResultHeaderLength + entries.Count * ForceOutEntryLength);
        w.WriteZero(RequestHeaderLength);
        w.WriteByte((byte)entries.Count);
        w.WriteByte(code);
        foreach (var (entry, status) in entries)
        {
            if (entry.Length != ForceOutEntryLength) throw new ArgumentException($"A force-out entry is {ForceOutEntryLength} bytes.", nameof(entries));
            var copy = (byte[])entry.Clone();
            BinaryPrimitives.WriteUInt32LittleEndian(copy.AsSpan(ForceOutEntryStatusOffset), status);
            w.WriteBytes(copy);
        }
        return w.ToArray();
    }

    public static byte[] AnswerApplyResultFor(uint code, bool isError, ulong applicantId, uint answer)
    {
        var w = new PacketWriter(AnswerApplyResultLength);
        w.WriteUInt32(code);
        w.WriteUInt32(isError ? 1u : 0u);
        w.WriteUInt64(applicantId);
        w.WriteUInt32(answer);
        return w.ToArray();
    }

    public static byte[] DissolveResultFor(bool failed, byte reason) => new[] { failed ? Failed : Ok, failed ? reason : (byte)0 };

    public static byte[] DetailFor(GuildDetailView detail, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(1 + DetailLength);
        w.WriteByte(Ok);
        WriteText(w, detail.GuildName, GuildNameLength, text);
        WriteText(w, detail.MasterName, DetailMasterNameLength, text);
        w.WriteInt32(detail.GuildPoint);
        w.WriteZero(0x58 - 0x40);
        w.WriteInt32(detail.MemberCount);
        w.WriteZero(0xC1 - 0x5C);
        WriteText(w, detail.Intro, DetailIntroLength, text);
        w.WriteUInt32(detail.GuildCode);
        w.WriteZero(DetailLength - 0x108);
        return w.ToArray();
    }

    public static byte[] DetailRefused(byte code) => new[] { code };

    public static byte[] NameTagFor(ushort entityId, string guildName, string alias, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(alias);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(16 + guildName.Length + alias.Length);
        w.WriteUInt16(entityId);
        w.WriteNulTerminated(guildName, text);
        w.WriteNulTerminated(alias, text);
        w.WriteByte(0);
        w.WriteByte(0);
        w.WriteUInt32(0);
        w.WriteByte(0);
        return w.ToArray();
    }

    public static byte[] NameOrderFor(string guildName, string alias, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(alias);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(NameOrderLength);
        WriteText(w, guildName, GuildNameLength, text);
        WriteText(w, alias, AliasLength, text);
        return w.ToArray();
    }

    public static (string GuildName, string Alias) ReadNameOrder(ReadOnlySpan<byte> data, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (data.Length < NameOrderLength) throw new ArgumentException($"A guild name order is {NameOrderLength} bytes.", nameof(data));
        return (ReadText(data, 0, GuildNameLength, text), ReadText(data, GuildNameLength, AliasLength, text));
    }

    public static ulong? ReadOwnInfoRequest(ReadOnlySpan<byte> payload) =>
        payload.Length < OwnInfoRequestLength ? null : BinaryPrimitives.ReadUInt64LittleEndian(payload);

    public static GuildCreateRequest? ReadCreateRequest(ReadOnlySpan<byte> payload, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (payload.Length < CreateRequestLength) return null;
        return new GuildCreateRequest(ReadText(payload, 0, GuildNameLength, text), ReadText(payload, 0x2D, IntroLength, text));
    }

    public static uint? ReadApplyRequest(ReadOnlySpan<byte> payload) =>
        payload.Length < ApplyRequestLength ? null : BinaryPrimitives.ReadUInt32LittleEndian(payload);

    public static uint? ReadMemberListRequest(ReadOnlySpan<byte> payload) =>
        payload.Length < MemberListRequestLength ? null : BinaryPrimitives.ReadUInt32LittleEndian(payload[RequestHeaderLength..]);

    public static (ulong ApplicantId, uint Answer)? ReadAnswerApplyRequest(ReadOnlySpan<byte> payload) =>
        payload.Length < AnswerApplyRequestLength
            ? null
            : (BinaryPrimitives.ReadUInt64LittleEndian(payload[RequestHeaderLength..]), BinaryPrimitives.ReadUInt32LittleEndian(payload[0x14..]));

    public static IReadOnlyList<byte[]>? ReadForceOutRequest(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < ForceOutHeaderLength) return null;
        var count = payload[RequestHeaderLength];
        if (payload.Length < ForceOutHeaderLength + count * ForceOutEntryLength) return null;
        var entries = new byte[count][];
        for (var i = 0; i < count; i++)
        {
            entries[i] = payload.Slice(ForceOutHeaderLength + i * ForceOutEntryLength, ForceOutEntryLength).ToArray();
        }
        return entries;
    }

    public static ulong ForceOutTarget(ReadOnlySpan<byte> entry) =>
        BinaryPrimitives.ReadUInt64LittleEndian(entry[ForceOutEntryUidOffset..]);

    public static uint? ReadDetailByCodeRequest(ReadOnlySpan<byte> payload) =>
        payload.Length < DetailByCodeRequestLength ? null : BinaryPrimitives.ReadUInt32LittleEndian(payload);

    public static string? ReadDetailByCharacterRequest(ReadOnlySpan<byte> payload, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return payload.Length < DetailByCharacterRequestLength ? null : ReadText(payload, 0, CharacterNameLength, text);
    }

    public static string ReadText(ReadOnlySpan<byte> payload, int offset, int field, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var slot = payload.Slice(offset, field);
        var end = slot.IndexOf((byte)0);
        return text.GetString(end < 0 ? slot : slot[..end]);
    }

    private static void WriteBlock(PacketWriter w, GuildBlock block, Encoding text)
    {
        w.WriteUInt64(block.CharacterId);
        w.WriteUInt32(block.GuildCode);
        WriteText(w, block.GuildName, BlockGuildNameLength, text, terminated: false);
        w.WriteZero(0x2E - 0x24);
        WriteText(w, block.MasterName, CharacterNameLength, text);
        WriteText(w, block.Greeting, GreetingLength, text);
        w.WriteByte(0);
    }

    private static void WriteGradeRights(PacketWriter w, byte gradeIndex, ushort rights)
    {
        for (var grade = 0; grade < GradeCount; grade++)
        {
            var own = grade == gradeIndex;
            w.WriteByte(own && (rights & RightEditMembers) != 0 ? (byte)1 : (byte)0);
            w.WriteByte(own && (rights & RightVolunteers) != 0 ? (byte)1 : (byte)0);
            w.WriteZero(GradeRightsStride - 2);
        }
    }

    private static void WriteGradeTable(PacketWriter w, IReadOnlyList<string> gradeNames, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(gradeNames);
        w.WriteZero(GradeTableHeadLength);
        for (var grade = 0; grade < GradeCount; grade++)
        {
            WriteText(w, grade < gradeNames.Count ? gradeNames[grade] : string.Empty, GradeNameLength, text);
        }
        w.WriteZero(GradeTableLength - GradeTableHeadLength - GradeCount * GradeNameLength);
    }

    private static void WriteText(PacketWriter w, string value, int field, Encoding text, bool terminated = true)
    {
        var limit = terminated ? field - 1 : field;
        var length = value.Length;
        while (length > 0 && text.GetByteCount(value.AsSpan(0, length)) > limit) length--;
        var bytes = text.GetBytes(value[..length]);
        w.WriteBytes(bytes);
        w.WriteZero(field - bytes.Length);
    }
}
