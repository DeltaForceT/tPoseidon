using System.Text;

namespace tPoseidon.Core.Net.Social;

public static class GameMasterFrames
{
    public const ushort ToolConnect = 0x1451;
    public const ushort SessionOpen = 0x1452;
    public const ushort Login = 0x1454;
    public const ushort LoginOk = 0x1455;
    public const ushort Logout = 0x1457;
    public const ushort Speed = 0x1458;
    public const ushort SpeedOk = 0x1459;
    public const ushort TeleportToChr = 0x145B;
    public const ushort TeleportToChrOk = 0x145C;
    public const ushort TeleportToChrFail = 0x145D;
    public const ushort TeleportToZone = 0x145E;
    public const ushort TeleportToZoneFail = 0x1460;
    public const ushort Immortal = 0x1461;
    public const ushort ImmortalOk = 0x1462;
    public const ushort Invisible = 0x1464;
    public const ushort InvisibleOk = 0x1465;
    public const ushort SuperNotice = 0x1467;
    public const ushort SuperNoticeShown = 0x1792;
    public const ushort KickRequest = 0x146A;
    public const ushort AccountBlock = 0x146D;
    public const ushort AccountBlockOk = 0x146E;
    public const ushort AccountBlockFail = 0x146F;
    public const byte AccountBlockOn = 1;
    public const ushort SummonMonsterOk = 0x14A8;
    public const ushort SummonMonsterFail = 0x14A9;
    public const ushort SetMusicOk = 0x14AE;
    public const ushort SetMusicFail = 0x14AF;
    public const ushort RemoveObjectOk = 0x14B7;
    public const ushort RemoveObjectFail = 0x14B8;
    public const ushort KickOk = 0x146B;
    public const ushort KickFail = 0x146C;
    public const ushort ZoneCounts = 0x1470;
    public const ushort ZoneCountsReply = 0x1471;
    public const ushort CharCount = 0x1476;
    public const ushort CharCountReply = 0x1477;
    public const ushort CharInfo = 0x1479;
    public const ushort CharInfoReply = 0x147A;
    public const ushort CharInfoMissing = 0x147B;
    public const ushort PresentItem = 0x147F;
    public const byte PresentToCashBox = 1;
    public const byte PresentToBagForHours = 2;
    public const int PresentDeliveryLength = sizeof(byte) + sizeof(ushort);
    public const ushort PresentItemOk = 0x150A;
    public const ushort PresentItemFail = 0x1481;
    public const ushort RemoveMyCampById = 0x149C;
    public const ushort RemoveMyCampUnknownZone = 0x149E;
    public const ushort RemoveMyCampByName = 0x149F;
    public const ushort RemovePersonalShop = 0x14A0;
    public const ushort RemoveInstantChat = 0x14A3;
    public const ushort InstantChatRemoved = 0x14A4;
    public const ushort InstantChatRemoveFail = 0x14A5;
    public const ushort SummonMonster = 0x14A7;
    public const ushort SetIssueItem = 0x14AA;
    public const ushort SetIssueItemOk = 0x14AB;
    public const ushort SetIssueItemFail = 0x14AC;
    public const ushort SetMusic = 0x14AD;
    public const ushort RemoveMonsters = 0x14B0;
    public const ushort SummonNpc = 0x14B3;
    public const ushort SummonMonsterArea = 0x1505;
    public const ushort RemoveOwnMonsters = 0x1506;
    public const ushort GameFlag = 0x1507;
    public const ushort GameFlagOk = 0x1508;
    public const ushort GameFlagFail = 0x1509;
    public const ushort Fly = 0x1530;
    public const ushort FlyOk = 0x1531;
    public const ushort FlyFail = 0x1532;
    public const ushort LearnSkill = 0x1533;
    public const ushort LearnSkillOk = 0x1534;
    public const ushort LearnSkillFail = 0x1535;
    public const ushort SetLevel = 0x1536;
    public const ushort SetLevelOk = 0x1537;
    public const ushort SetLevelFail = 0x1538;
    public const ushort MegaphoneNotice = 0x1539;
    public const ushort MegaphoneNoticeOk = 0x153A;
    public const ushort MegaphoneNoticeFail = 0x153B;
    public const byte NoticeRefusedEmpty = 1;
    public const byte NoticeRefusedTooLong = 2;
    public const byte NoticeRefusedNotSaved = 3;
    public const char NoticeHeaderOpen = '[';
    public const char NoticeHeaderClose = ']';
    public const ushort SummonNpcOk = 0x14B4;
    public const ushort SummonNpcFail = 0x14B5;
    public const ushort RemoveObject = 0x14B6;
    public const ushort NpcActivate = 0x14DE;
    public const ushort NpcActivated = 0x14DF;
    public const ushort NpcActivateFail = 0x14E0;
    public const ushort NpcDeactivate = 0x14E1;
    public const ushort NpcDeactivated = 0x14E2;
    public const ushort NpcDeactivateFail = 0x14E3;
    public const ushort NpcList = 0x14E4;
    public const ushort NpcListReply = 0x14E5;
    public const ushort NpcRefresh = 0x14E7;
    public const ushort NpcRefreshed = 0x14E8;
    public const ushort NpcRefreshFail = 0x14E9;
    public const ushort KickWithCause = 0x1482;
    public const ushort BlockWithCause = 0x1485;
    public const ushort BlockWithCauseOk = 0x1486;
    public const ushort BlockWithCauseFail = 0x1487;
    public const ushort BlockWithCauseNotOnline = 1;
    public const ushort ClientBlockReleased = 0x1489;
    public const ushort ClientBlockList = 0x148A;
    public const ushort ClientCauseRead = 0x1491;
    public const ushort ClientCause = 0x217C;
    public const ushort ClientKickCauseRead = 0x1488;
    public const ushort ClientKickCause = 0x2178;
    public const ushort Block = 0x148B;
    public const ushort BlockOk = 0x148C;
    public const ushort BlockFail = 0x148D;
    public const ushort Unblock = 0x148E;
    public const ushort UnblockOk = 0x148F;
    public const ushort UnblockFail = 0x1490;
    public const ushort BlockList = 0x1492;
    public const ushort BlockListReply = 0x1493;
    public const ushort BlockListAll = 0x1494;
    public const ushort BlockListByTime = 0x1495;
    public const ushort BlockListAllReply = 0x1496;
    public const ushort ClientUnblocked = 0x2905;
    public const ushort ClientBlocked = 0x2906;
    public const ushort Memo = 0x1497;
    public const ushort OxQuestion = 0x14B9;
    public const ushort OxAnswer = 0x14BC;
    public const ushort OxGiveItem = 0x14C3;
    public const ushort OxGiveItemOk = 0x14C4;
    public const ushort OxGiveItemFail = 0x14C5;
    public const ushort MusicChange = 0x024C;
    public const ushort OxNotice = 0x0279;
    public const ushort OxTimerSound = 0x02E5;
    public const ushort OxAnswerSound = 0x02E4;
    public const ushort GuildInfo = 0x14C6;
    public const ushort GuildInfoReply = 0x14C7;
    public const ushort GuildInfoMissing = 0x14C8;
    public const ushort GuildGradeMemo = 0x14C9;
    public const ushort GuildMemoFail = 0x14CB;
    public const ushort GuildBlock = 0x14D2;
    public const ushort GuildBlockOk = 0x14D3;
    public const ushort GuildBlockFail = 0x14D4;
    public const byte GuildBlockUnknownGuild = 0;
    public const byte GuildBlockNotLoaded = 1;
    public const ushort GuildDissolve = 0x14DB;
    public const ushort GuildDissolveOk = 0x14DC;
    public const ushort GuildDissolveFail = 0x14DD;
    public const string GuildCreatedFormat = "yyyyMMdd";
    public const ushort GuildMemoToMaster = 0x14CC;
    public const ushort GuildMemoToAllMasters = 0x14CF;
    public const ushort GuildToZone = 0x14D5;
    public const ushort GuildToChr = 0x14D8;
    public const ushort GuildGameFlag = 0x14BF;
    public const ushort SetGuildGameFlag = 0x14C0;
    public const ushort GuildGameFlagReply = 0x14C1;
    public const ushort GuildRoomFlag = 0x14EA;
    public const ushort SetGuildRoomFlag = 0x14EB;
    public const ushort GuildRoomFlagReply = 0x14EC;

    public const ushort OrderSuperRun = 0x5B;
    public const ushort OrderCharInfo = 0x64;
    public const ushort ReportCharInfo = 0x61;
    public const ushort ReportSummonMonster = 0x63;
    public const ushort ReportSetIssueItem = 0x64;
    public const ushort ReportOxGiveItem = 0x68;
    public const ushort ReportSetMusic = 0x65;
    public const ushort ReportRemoveObject = 0x67;
    public const ushort ReportNpcOnOff = 0x69;
    public const ushort ReportNpcRefresh = 0x6A;
    public const ushort ReportSummonNpc = 0x66;
    public const ushort ReportPresentItem = 0x5E;
    public const ushort ReportSetFlight = 0x6B;
    public const ushort ReportLearnSkill = 0x6C;
    public const ushort ReportSetLevel = 0x6D;
    public const int NpcOnOffAckLength = 15;
    public const uint AckOk = 0;
    public const uint AckBadMonsterType = 1;
    public const uint AckBadZone = 2;
    public const uint AckFailed = 4;
    public const uint AckRemoveObjectFailed = 1;
    public const uint AckNoSuchNpc = 1;
    public const uint AckNotOxMap = 1;
    public const uint AckNoGameMaster = 2;
    public const uint AckSummonNpcNoGameMaster = 1;
    public const uint AckGiveFailed = 4;
    public const uint AckPresentUnknownItem = 1;
    public const uint AckPresentNoReceiver = 2;
    public const uint AckPresentNotOnline = 3;
    public const uint AckPresentNoRoom = AckGiveFailed;
    public const uint AckPresentBoxFailed = 5;
    public const uint AckFlightNoGmFlag = 1;
    public const uint AckFlightNotInGame = 2;
    public const uint AckLearnUnknownSkill = 1;
    public const uint AckLearnBadLevel = 2;
    public const uint AckLearnNotOnline = 3;
    public const uint AckLearnNotHigher = 4;
    public const uint AckLevelNotOnline = 1;
    public const uint AckLevelInvalid = 2;
    public const uint AckLevelLower = 3;
    public const uint AckLevelSame = 4;
    public const uint AckLevelNoChart = 5;
    public const ushort OrderPresentItem = 0x61;
    public const ushort OrderRemoveMyCamp = 0x63;
    public const ushort OrderRemovePersonalShop = 0x65;
    public const ushort OrderSummonMonster = 0x66;
    public const ushort OrderSummonNpc = 0x69;
    public const ushort OrderSummonMonsterArea = 0x70;
    public const ushort OrderRemoveOwnMonsters = 0x71;
    public const ushort OrderSetIssueItem = 0x67;
    public const ushort OrderSetMusic = 0x68;
    public const ushort OrderRemoveObject = 0x6A;
    public const ushort OrderNpcOnOff = 0x6E;
    public const ushort OrderNpcRefresh = 0x6F;
    public const ushort OrderOxQuestion = 0x6B;
    public const ushort OrderOxAnswer = 0x6C;
    public const ushort OrderOxGiveItem = 0x6D;
    public const ushort OrderTeleportToZone = 0x5D;
    public const ushort OrderTeleportToChr = 0x5E;
    public const ushort OrderImmortal = 0x5F;
    public const ushort OrderInvisible = 0x60;
    public const ushort OrderSetGmAttribute = 0x62;
    public const ushort OrderSetFlight = 0x72;
    public const ushort OrderLearnSkill = 0x73;
    public const ushort OrderSetLevel = 0x74;

    public const int MemoLength = 35;
    public const ushort FirstIndex = 1;
    public const ushort LastIndex = 0xFFFD;

    public static ushort NextIndex(ushort current) => current >= LastIndex ? FirstIndex : (ushort)(current + 1);
    public const int CharInfoNameLength = 0x20;
    public const int CharInfoReportLength = 0x34;
    public const int MusicNameLength = 64;
    public const uint FirstSummonableMonster = 2000;
    public const uint LastSummonableMonster = 3434;

    public const byte NormalSpeed = 1;
    public const byte MaxSpeed = 4;

    public const ushort FirstZone = 1;
    public const ushort ZoneLimit = 1000;

    public const ushort KickCharacter = 0x1596;

    public const ushort LinkChatCommand = 0x1601;
    public const ushort LinkChatReply = 0x1602;
    public const ushort LinkTeleportRelay = 0x1603;

    public const int PasswordBufferSize = 32;

    public static bool TryReadPassword(ReadOnlySpan<byte> payload, out byte[] password, out int consumed)
    {
        password = Array.Empty<byte>();
        consumed = 0;
        if (payload.Length < 1) return false;
        var length = payload[0];
        if (length >= PasswordBufferSize || payload.Length < 1 + length) return false;
        password = payload.Slice(1, length).ToArray();
        consumed = 1 + length;
        return true;
    }

    private static readonly HashSet<ushort> GameServerOrders = new()
    {
        OrderSuperRun,
        OrderCharInfo,
        OrderPresentItem,
        OrderSetGmAttribute,
        OrderRemoveMyCamp,
        OrderRemovePersonalShop,
        OrderSummonMonster,
        OrderSummonNpc,
        OrderSummonMonsterArea,
        OrderRemoveOwnMonsters,
        OrderSetIssueItem,
        OrderSetMusic,
        OrderRemoveObject,
        OrderOxQuestion,
        OrderOxAnswer,
        OrderOxGiveItem,
        OrderTeleportToZone,
        OrderTeleportToChr,
        OrderImmortal,
        OrderInvisible,
        OrderSetFlight,
        OrderLearnSkill,
        OrderSetLevel,
    };

    public static ushort? ReportOf(ushort orderType) => orderType switch
    {
        OrderCharInfo => ReportCharInfo,
        OrderPresentItem => ReportPresentItem,
        OrderSummonMonster => ReportSummonMonster,
        OrderSummonNpc => ReportSummonNpc,
        OrderSummonMonsterArea => ReportSummonMonster,
        OrderRemoveOwnMonsters => ReportRemoveObject,
        OrderSetIssueItem => ReportSetIssueItem,
        OrderOxGiveItem => ReportOxGiveItem,
        OrderSetMusic => ReportSetMusic,
        OrderRemoveObject => ReportRemoveObject,
        OrderNpcOnOff => ReportNpcOnOff,
        OrderNpcRefresh => ReportNpcRefresh,
        OrderSetFlight => ReportSetFlight,
        OrderLearnSkill => ReportLearnSkill,
        OrderSetLevel => ReportSetLevel,
        _ => null,
    };

    public const int IssueItemLength = 8;

    public static byte[] IssueItem(ushort objectId, uint itemId, ushort count)
    {
        var w = new PacketWriter(IssueItemLength);
        w.WriteUInt16(objectId);
        w.WriteUInt32(itemId);
        w.WriteUInt16(count);
        return w.ToArray();
    }

    public static bool TryReadIssueItem(ReadOnlySpan<byte> data, out ushort objectId, out uint itemId, out ushort count)
    {
        objectId = 0;
        itemId = 0;
        count = 0;
        if (data.Length < IssueItemLength) return false;
        var r = new PacketReader(data);
        objectId = r.ReadUInt16();
        itemId = r.ReadUInt32();
        count = r.ReadUInt16();
        return true;
    }

    public const int OxGiveItemLength = 5;

    public static byte[] OxGift(uint itemId, byte count)
    {
        var w = new PacketWriter(OxGiveItemLength);
        w.WriteUInt32(itemId);
        w.WriteByte(count);
        return w.ToArray();
    }

    public static bool TryReadOxGift(ReadOnlySpan<byte> data, out uint itemId, out byte count)
    {
        itemId = 0;
        count = 0;
        if (data.Length < OxGiveItemLength) return false;
        var r = new PacketReader(data);
        itemId = r.ReadUInt32();
        count = r.ReadByte();
        return true;
    }

    public static bool TryReadBlock(ReadOnlySpan<byte> data, Encoding text, out ushort type, out uint minutes, out string name)
    {
        type = 0;
        minutes = 0;
        name = string.Empty;
        if (data.Length < 7 || data[6..].IndexOf((byte)0) < 0) return false;
        var r = new PacketReader(data);
        type = r.ReadUInt16();
        minutes = r.ReadUInt32();
        name = r.ReadNulTerminated(text);
        return true;
    }

    public static bool TryReadBlockWithCause(ReadOnlySpan<byte> data, Encoding text, out string name, out uint minutes, out string cause)
    {
        name = string.Empty;
        minutes = 0;
        cause = string.Empty;
        var nameEnd = data.IndexOf((byte)0);
        if (nameEnd < 0 || data.Length < nameEnd + 1 + sizeof(uint) + 1) return false;
        var rest = data[(nameEnd + 1 + sizeof(uint))..];
        if (rest.IndexOf((byte)0) < 0) return false;
        var r = new PacketReader(data);
        name = r.ReadNulTerminated(text);
        minutes = r.ReadUInt32();
        cause = r.ReadNulTerminated(text);
        return true;
    }

    public static byte[] ClientCauseBody(string cause, ushort gameMasterIndex, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(cause);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(text.GetByteCount(cause) + 1 + sizeof(ushort));
        w.WriteNulTerminated(cause, text);
        w.WriteUInt16(gameMasterIndex);
        return w.ToArray();
    }

    public static bool TryReadCauseRead(ReadOnlySpan<byte> data, out ushort gameMasterIndex)
    {
        gameMasterIndex = 0;
        if (data.Length < sizeof(uint)) return false;
        gameMasterIndex = (ushort)new PacketReader(data).ReadUInt32();
        return true;
    }

    public static bool TryReadClientBlockList(ReadOnlySpan<byte> data, out IReadOnlyList<ushort> types)
    {
        types = Array.Empty<ushort>();
        if (data.Length < sizeof(ushort)) return false;
        var r = new PacketReader(data);
        var count = r.ReadUInt16();
        if (data.Length < sizeof(ushort) * (1 + count)) return false;
        var list = new ushort[count];
        for (var i = 0; i < count; i++) list[i] = r.ReadUInt16();
        types = list;
        return true;
    }

    public static bool TryReadUnblock(ReadOnlySpan<byte> data, Encoding text, out ushort type, out string name)
    {
        type = 0;
        name = string.Empty;
        if (data.Length < 3 || data[2..].IndexOf((byte)0) < 0) return false;
        var r = new PacketReader(data);
        type = r.ReadUInt16();
        name = r.ReadNulTerminated(text);
        return true;
    }

    public static byte[] BlockType(ushort type)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(type);
        return w.ToArray();
    }

    public static byte[] ClientBlockedBody(ushort type, uint minutes)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(type);
        w.WriteUInt32(minutes);
        return w.ToArray();
    }

    public static byte[] BlockListBody(IReadOnlyList<GameMasterBlockEntry> entries, Func<ulong, string?> onlineName, Encoding text, bool withType)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(onlineName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(4 + entries.Count * 64);
        w.WriteUInt32((uint)entries.Count);
        foreach (var entry in entries)
        {
            var current = onlineName(entry.CharacterId);
            w.WriteNulTerminated(current ?? entry.Name, text);
            w.WriteUInt64(entry.CharacterId);
            w.WriteUInt16(current is not null ? (ushort)0 : (ushort)1);
            if (withType) w.WriteByte(entry.Type);
            w.WriteUInt32(entry.Minutes);
            w.WriteNulTerminated(entry.Time, text);
        }
        return w.ToArray();
    }

    public static bool TryReadBlockList(ReadOnlySpan<byte> data, Encoding text, bool withType, out IReadOnlyList<GameMasterBlockEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(text);
        entries = Array.Empty<GameMasterBlockEntry>();
        if (data.Length < sizeof(uint)) return false;
        var r = new PacketReader(data);
        var count = r.ReadUInt32();
        var list = new List<GameMasterBlockEntry>();
        try
        {
            for (var i = 0; i < count; i++)
            {
                var name = r.ReadNulTerminated(text);
                var characterId = r.ReadUInt64();
                r.ReadUInt16();
                var type = withType ? r.ReadByte() : (byte)0;
                var minutes = r.ReadUInt32();
                list.Add(new GameMasterBlockEntry(type, characterId, name, minutes, r.ReadNulTerminated(text)));
            }
        }
        catch (tPoseidon.Core.Common.InvalidPacketException)
        {
            return false;
        }
        entries = list;
        return true;
    }

    public sealed record GuildInfoReport(uint GuildCode, string GuildName, byte MemberCount, string MasterName, string Created, bool MasterOnline, IReadOnlyList<string> OnlineMembers);

    public static byte[] GuildInfoBody(GuildInfoReport info, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(96 + info.OnlineMembers.Count * 24);
        w.WriteUInt32(info.GuildCode);
        w.WriteNulTerminated(info.GuildName, text);
        w.WriteByte(info.MemberCount);
        w.WriteNulTerminated(info.MasterName, text);
        w.WriteNulTerminated(info.Created, text);
        var loaded = info.OnlineMembers.Count > 0;
        w.WriteByte(loaded ? (byte)1 : (byte)0);
        if (!loaded) return w.ToArray();
        w.WriteByte(info.MasterOnline ? (byte)1 : (byte)0);
        w.WriteByte((byte)Math.Min(info.OnlineMembers.Count, byte.MaxValue));
        foreach (var name in info.OnlineMembers.Take(byte.MaxValue)) w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static bool TryReadGuildGradeMemo(ReadOnlySpan<byte> data, Encoding text, out byte grade, out string guild, out string memo)
    {
        grade = 0;
        guild = string.Empty;
        memo = string.Empty;
        if (data.Length < 3) return false;
        try
        {
            var r = new PacketReader(data);
            grade = r.ReadByte();
            guild = r.ReadNulTerminated(text);
            memo = r.ReadNulTerminated(text);
        }
        catch (tPoseidon.Core.Common.InvalidPacketException)
        {
            return false;
        }
        return true;
    }

    public static bool TryReadGuildBlock(ReadOnlySpan<byte> data, Encoding text, out ushort type, out uint minutes, out string guild, out string reason)
    {
        type = 0;
        minutes = 0;
        guild = string.Empty;
        reason = string.Empty;
        if (data.Length < 8) return false;
        try
        {
            var r = new PacketReader(data);
            type = r.ReadUInt16();
            minutes = r.ReadUInt32();
            guild = r.ReadNulTerminated(text);
            reason = r.ReadNulTerminated(text);
        }
        catch (tPoseidon.Core.Common.InvalidPacketException)
        {
            return false;
        }
        return true;
    }

    public static byte[] GuildName(string guild, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(guild);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(guild.Length * 2 + 1);
        w.WriteNulTerminated(guild, text);
        return w.ToArray();
    }

    public static byte[] GuildBlockFailBody(byte reason, string guild, Encoding text)
    {
        var w = new PacketWriter(guild.Length * 2 + 2);
        w.WriteByte(reason);
        w.WriteNulTerminated(guild, text);
        return w.ToArray();
    }

    public static byte[] AckResult(uint result)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(result);
        return w.ToArray();
    }

    public static bool TryReadAckResult(ReadOnlySpan<byte> data, out uint result)
    {
        result = 0;
        if (data.Length < sizeof(uint)) return false;
        result = new PacketReader(data).ReadUInt32();
        return true;
    }

    public static bool IsGameServerOrder(ushort orderType) => GameServerOrders.Contains(orderType);

    public static byte[] TwoNames(string first, string second, System.Text.Encoding text)
    {
        var w = new PacketWriter(2 + first.Length * 2 + second.Length * 2);
        w.WriteNulTerminated(first, text);
        w.WriteNulTerminated(second, text);
        return w.ToArray();
    }

    public static byte[] NameAndZone(string name, ushort zone, System.Text.Encoding text)
    {
        var w = new PacketWriter(3 + name.Length * 2);
        w.WriteNulTerminated(name, text);
        w.WriteUInt16(zone);
        return w.ToArray();
    }

    public static byte[] Zone(ushort zone)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(zone);
        return w.ToArray();
    }

    public static bool TryReadZone(ReadOnlySpan<byte> data, out ushort zone)
    {
        zone = 0;
        if (data.Length < sizeof(ushort)) return false;
        zone = new PacketReader(data).ReadUInt16();
        return true;
    }

    public const int ChrToChrLength = 22;

    public static byte[] ChrToChr(ulong who, ulong to, ushort zone = 0, ushort x = 0, ushort y = 0)
    {
        var w = new PacketWriter(ChrToChrLength);
        w.WriteUInt64(who);
        w.WriteUInt64(to);
        w.WriteUInt16(zone);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        return w.ToArray();
    }

    public static bool TryReadChrToChr(ReadOnlySpan<byte> data, out ulong who, out ulong to) =>
        TryReadChrToChr(data, out who, out to, out _, out _, out _);

    public static bool TryReadChrToChr(ReadOnlySpan<byte> data, out ulong who, out ulong to, out ushort zone, out ushort x, out ushort y)
    {
        who = 0;
        to = 0;
        zone = 0;
        x = 0;
        y = 0;
        if (data.Length < 2 * sizeof(ulong)) return false;
        var r = new PacketReader(data);
        who = r.ReadUInt64();
        to = r.ReadUInt64();
        if (data.Length < ChrToChrLength) return true;
        zone = r.ReadUInt16();
        x = r.ReadUInt16();
        y = r.ReadUInt16();
        return true;
    }

    public static byte[] CharacterId(ulong characterId) => Kick(characterId);

    public static bool TryReadCharacterId(ReadOnlySpan<byte> data, out ulong characterId) => TryReadKick(data, out characterId);

    public static byte[] Kick(ulong characterId)
    {
        var w = new PacketWriter(sizeof(ulong));
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static bool TryReadKick(ReadOnlySpan<byte> payload, out ulong characterId)
    {
        characterId = 0;
        if (payload.Length < sizeof(ulong)) return false;
        characterId = new PacketReader(payload).ReadUInt64();
        return true;
    }

    public static byte[] ChatLine(ulong characterId, string line, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(16 + line.Length * 2);
        w.WriteUInt64(characterId);
        w.WriteNulTerminated(line, text);
        return w.ToArray();
    }

    public static byte[] ChatCommand(ulong characterId, string name, string address, string line, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(24 + (name.Length + address.Length + line.Length) * 2);
        w.WriteUInt64(characterId);
        w.WriteNulTerminated(name, text);
        w.WriteNulTerminated(address, text);
        w.WriteNulTerminated(line, text);
        return w.ToArray();
    }

    public static bool TryReadChatCommand(ReadOnlySpan<byte> payload, System.Text.Encoding text, out ChatCommandFrame frame)
    {
        ArgumentNullException.ThrowIfNull(text);
        frame = default;
        if (payload.Length < sizeof(ulong) + 3) return false;
        var rest = payload[sizeof(ulong)..];
        var nameEnd = rest.IndexOf((byte)0);
        if (nameEnd < 0) return false;
        var afterName = rest[(nameEnd + 1)..];
        var addressEnd = afterName.IndexOf((byte)0);
        if (addressEnd < 0) return false;
        var afterAddress = afterName[(addressEnd + 1)..];
        var lineEnd = afterAddress.IndexOf((byte)0);
        if (lineEnd < 0) return false;
        frame = new ChatCommandFrame(
            new PacketReader(payload).ReadUInt64(),
            text.GetString(rest[..nameEnd]),
            text.GetString(afterName[..addressEnd]),
            text.GetString(afterAddress[..lineEnd]));
        return true;
    }

    public static bool TryReadChatLine(ReadOnlySpan<byte> payload, System.Text.Encoding text, out ulong characterId, out string line)
    {
        ArgumentNullException.ThrowIfNull(text);
        characterId = 0;
        line = string.Empty;
        if (payload.Length < sizeof(ulong) + 1 || payload[sizeof(ulong)..].IndexOf((byte)0) < 0) return false;
        var r = new PacketReader(payload);
        characterId = r.ReadUInt64();
        line = r.ReadNulTerminated(text);
        return true;
    }

    public static byte[] GmAttribute(bool on) => new[] { on ? (byte)1 : (byte)0 };

    public static bool TryReadGmAttribute(ReadOnlySpan<byte> data, out bool on)
    {
        on = false;
        if (data.Length < 1) return false;
        on = data[0] != 0;
        return true;
    }

    public static byte[] SuperRun(uint speed)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(speed);
        return w.ToArray();
    }

    public static bool TryReadSuperRun(ReadOnlySpan<byte> data, out uint speed)
    {
        speed = 0;
        if (data.Length < sizeof(uint)) return false;
        speed = new PacketReader(data).ReadUInt32();
        return true;
    }

    public static byte[] PresentFail(uint itemId, ushort count, string name, byte reason, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(sizeof(uint) + sizeof(ushort) + name.Length + 2);
        w.WriteUInt32(itemId);
        w.WriteUInt16(count);
        w.WriteNulTerminated(name, text);
        w.WriteByte(reason);
        return w.ToArray();
    }

    public static bool TryReadPresentFail(ReadOnlySpan<byte> payload, System.Text.Encoding text, out uint itemId, out ushort count, out string name, out byte reason)
    {
        itemId = 0;
        count = 0;
        name = string.Empty;
        reason = 0;
        if (payload.Length < sizeof(uint) + sizeof(ushort) + 2) return false;
        var r = new PacketReader(payload);
        itemId = r.ReadUInt32();
        count = r.ReadUInt16();
        name = r.ReadNulTerminated(text);
        reason = payload[^1];
        return true;
    }

    public static byte[] Present(ulong characterId, uint itemId, ushort count) => Present(characterId, itemId, count, PresentDelivery.Bag);

    public static byte[] Present(ulong characterId, uint itemId, ushort count, PresentDelivery delivery)
    {
        var w = new PacketWriter(sizeof(ulong) + sizeof(uint) + sizeof(ushort) + PresentDeliveryLength);
        w.WriteUInt64(characterId);
        w.WriteUInt32(itemId);
        w.WriteUInt16(count);
        if (delivery.IsCashBox || delivery.Hours != 0)
        {
            w.WriteByte(delivery.IsCashBox ? PresentToCashBox : PresentToBagForHours);
            w.WriteUInt16(delivery.Hours);
        }
        return w.ToArray();
    }

    public static bool TryReadPresent(ReadOnlySpan<byte> data, out ulong characterId, out uint itemId, out ushort count) =>
        TryReadPresent(data, out characterId, out itemId, out count, out _);

    public static bool TryReadPresent(ReadOnlySpan<byte> data, out ulong characterId, out uint itemId, out ushort count, out PresentDelivery delivery)
    {
        characterId = 0;
        itemId = 0;
        count = 0;
        delivery = PresentDelivery.Bag;
        if (data.Length < sizeof(ulong) + sizeof(uint) + sizeof(ushort)) return false;
        var r = new PacketReader(data);
        characterId = r.ReadUInt64();
        itemId = r.ReadUInt32();
        count = r.ReadUInt16();
        delivery = ReadPresentDelivery(data[(sizeof(ulong) + sizeof(uint) + sizeof(ushort))..]);
        return true;
    }

    public static PresentDelivery ReadPresentDelivery(ReadOnlySpan<byte> tail)
    {
        if (tail.Length < PresentDeliveryLength) return PresentDelivery.Bag;
        var hours = new PacketReader(tail[sizeof(byte)..]).ReadUInt16();
        return tail[0] switch
        {
            PresentToCashBox => PresentDelivery.CashBox(hours),
            PresentToBagForHours => PresentDelivery.BagFor(hours),
            _ => PresentDelivery.Bag,
        };
    }

    public static byte[] LearnSkillRequest(string name, ushort skillId, byte level, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(sizeof(ushort) + sizeof(byte) + name.Length + 1);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static bool TryReadLearnSkillRequest(ReadOnlySpan<byte> data, System.Text.Encoding text, out string name, out ushort skillId, out byte level)
    {
        ArgumentNullException.ThrowIfNull(text);
        name = string.Empty;
        skillId = 0;
        level = 0;
        if (data.Length < sizeof(ushort) + sizeof(byte) + 1 || data[^1] != 0) return false;
        var r = new PacketReader(data);
        skillId = r.ReadUInt16();
        level = r.ReadByte();
        name = r.ReadNulTerminated(text);
        return true;
    }

    public static byte[] LearnSkillOrder(ulong characterId, ushort skillId, byte level)
    {
        var w = new PacketWriter(sizeof(ulong) + sizeof(ushort) + sizeof(byte));
        w.WriteUInt64(characterId);
        w.WriteUInt16(skillId);
        w.WriteByte(level);
        return w.ToArray();
    }

    public static bool TryReadLearnSkillOrder(ReadOnlySpan<byte> data, out ulong characterId, out ushort skillId, out byte level)
    {
        characterId = 0;
        skillId = 0;
        level = 0;
        if (data.Length < sizeof(ulong) + sizeof(ushort) + sizeof(byte)) return false;
        var r = new PacketReader(data);
        characterId = r.ReadUInt64();
        skillId = r.ReadUInt16();
        level = r.ReadByte();
        return true;
    }

    public static byte[] SetLevelRequest(string name, ushort level, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(sizeof(ushort) + name.Length + 1);
        w.WriteUInt16(level);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static bool TryReadSetLevelRequest(ReadOnlySpan<byte> data, System.Text.Encoding text, out string name, out ushort level)
    {
        ArgumentNullException.ThrowIfNull(text);
        name = string.Empty;
        level = 0;
        if (data.Length < sizeof(ushort) + 1 || data[^1] != 0) return false;
        var r = new PacketReader(data);
        level = r.ReadUInt16();
        name = r.ReadNulTerminated(text);
        return true;
    }

    public static byte[] SetLevelOrder(ulong characterId, ushort level)
    {
        var w = new PacketWriter(sizeof(ulong) + sizeof(ushort));
        w.WriteUInt64(characterId);
        w.WriteUInt16(level);
        return w.ToArray();
    }

    public static bool TryReadSetLevelOrder(ReadOnlySpan<byte> data, out ulong characterId, out ushort level)
    {
        characterId = 0;
        level = 0;
        if (data.Length < sizeof(ulong) + sizeof(ushort)) return false;
        var r = new PacketReader(data);
        characterId = r.ReadUInt64();
        level = r.ReadUInt16();
        return true;
    }

    public const string OxTimerQuestion = "5";
    public const char OxAnswerSeparator = ' ';
    public const string OxAnswerCircle = "O";
    public const string OxAnswerCross = "X";
    public const byte OxAnswerCircleCode = 0;

    public static bool IsOxTimerQuestion(string question) =>
        string.Equals(question, OxTimerQuestion, StringComparison.OrdinalIgnoreCase);

    public static string OxAnswerText(string answerWord, byte answer) =>
        answerWord + OxAnswerSeparator + (answer == OxAnswerCircleCode ? OxAnswerCircle : OxAnswerCross);

    public static byte[] OxNoticePacket(string text, System.Text.Encoding encoding) => MusicPacket(text, encoding);

    public static string ReadQuestion(ReadOnlySpan<byte> data, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var end = data.IndexOf((byte)0);
        return text.GetString(end < 0 ? data : data[..end]);
    }

    public static byte[] Toggle(bool on) => new[] { on ? (byte)1 : (byte)0 };

    public static bool TryReadToggle(ReadOnlySpan<byte> data, out bool on) => TryReadGmAttribute(data, out on);

    public static bool IsSummonableMonster(uint chrType) =>
        chrType is >= FirstSummonableMonster and <= LastSummonableMonster;

    public static byte[] Summon(uint chrType, uint zone, byte count)
    {
        var w = new PacketWriter(sizeof(uint) * 2 + sizeof(byte));
        w.WriteUInt32(chrType);
        w.WriteUInt32(zone);
        w.WriteByte(count);
        return w.ToArray();
    }

    public static bool TryReadSummon(ReadOnlySpan<byte> data, out uint chrType, out uint zone, out byte count)
    {
        chrType = 0;
        zone = 0;
        count = 0;
        if (data.Length < sizeof(uint) * 2 + sizeof(byte)) return false;
        var r = new PacketReader(data);
        chrType = r.ReadUInt32();
        zone = r.ReadUInt32();
        count = r.ReadByte();
        return true;
    }

    public const int SummonAreaLength = sizeof(uint) + sizeof(byte) + sizeof(ushort) + sizeof(byte) + sizeof(ushort) * 3;

    public static byte[] SummonArea(SummonAreaRequest request)
    {
        var w = new PacketWriter(SummonAreaLength);
        w.WriteUInt32(request.ChrType);
        w.WriteByte(request.Count);
        w.WriteUInt16(request.Radius);
        w.WriteByte((byte)request.Anchor);
        w.WriteUInt16(request.Map);
        w.WriteUInt16(request.X);
        w.WriteUInt16(request.Y);
        return w.ToArray();
    }

    public static bool TryReadSummonArea(ReadOnlySpan<byte> data, out SummonAreaRequest request)
    {
        request = default;
        if (data.Length < SummonAreaLength) return false;
        var r = new PacketReader(data);
        var chrType = r.ReadUInt32();
        var count = r.ReadByte();
        var radius = r.ReadUInt16();
        var anchor = r.ReadByte();
        if (!Enum.IsDefined((SummonAnchor)anchor)) return false;
        request = new SummonAreaRequest(chrType, count, radius, (SummonAnchor)anchor, r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16());
        return true;
    }

    public const ushort ZoneCountLimit = 1000;

    public static byte[] ZoneCountsBody(IEnumerable<ushort> zonesOfOnlineCharacters)
    {
        ArgumentNullException.ThrowIfNull(zonesOfOnlineCharacters);
        var counts = new ushort[ZoneCountLimit];
        foreach (var zone in zonesOfOnlineCharacters)
        {
            counts[zone < ZoneCountLimit ? zone : 0]++;
        }
        var filled = Enumerable.Range(0, ZoneCountLimit).Where(zone => counts[zone] != 0).ToArray();
        var w = new PacketWriter(sizeof(ushort) + filled.Length * sizeof(ushort) * 2);
        w.WriteUInt16((ushort)filled.Length);
        foreach (var zone in filled)
        {
            w.WriteUInt16((ushort)zone);
            w.WriteUInt16(counts[zone]);
        }
        return w.ToArray();
    }

    public static bool TryReadZoneCounts(ReadOnlySpan<byte> data, out IReadOnlyList<(ushort Zone, ushort Count)> zones)
    {
        zones = Array.Empty<(ushort, ushort)>();
        if (data.Length < sizeof(ushort)) return false;
        var r = new PacketReader(data);
        var n = r.ReadUInt16();
        if (data.Length < sizeof(ushort) + n * sizeof(ushort) * 2) return false;
        var list = new List<(ushort, ushort)>(n);
        for (var i = 0; i < n; i++) list.Add((r.ReadUInt16(), r.ReadUInt16()));
        zones = list;
        return true;
    }

    public static byte[] SummonNpcRequest(uint chrType, IReadOnlyList<string> lines, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(sizeof(uint) + 1 + lines.Sum(line => text.GetByteCount(line) + 1));
        w.WriteUInt32(chrType);
        w.WriteByte((byte)lines.Count);
        foreach (var line in lines) w.WriteNulTerminated(line, text);
        return w.ToArray();
    }

    public static bool TryReadSummonNpc(ReadOnlySpan<byte> data, System.Text.Encoding text, out uint chrType, out IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(text);
        chrType = 0;
        lines = Array.Empty<string>();
        if (data.Length < sizeof(uint) + 1) return false;
        var r = new PacketReader(data);
        chrType = r.ReadUInt32();
        var n = r.ReadByte();
        var read = new List<string>(n);
        var rest = data[(sizeof(uint) + 1)..];
        for (var i = 0; i < n; i++)
        {
            var end = rest.IndexOf((byte)0);
            if (end < 0) return false;
            read.Add(text.GetString(rest[..end]));
            rest = rest[(end + 1)..];
        }
        lines = read;
        return true;
    }

    public static byte[] MusicName(string file, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(text);
        var bytes = text.GetBytes(file);
        var buffer = new byte[MusicNameLength];
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, MusicNameLength - 1));
        return buffer;
    }

    public static string ReadMusicName(ReadOnlySpan<byte> data, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var limited = data.Length > MusicNameLength ? data[..MusicNameLength] : data;
        var end = limited.IndexOf((byte)0);
        return text.GetString(end < 0 ? limited : limited[..end]);
    }

    public static byte[] MusicPacket(string file, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(file.Length * 2 + 1);
        w.WriteNulTerminated(file, text);
        return w.ToArray();
    }

    public static byte[] ObjectId(ushort id) => Zone(id);

    public static bool TryReadObjectId(ReadOnlySpan<byte> data, out ushort id) => TryReadZone(data, out id);

    public static byte[] NpcOnOff(bool on, ushort objectId)
    {
        var w = new PacketWriter(sizeof(byte) + sizeof(ushort));
        w.WriteByte(on ? (byte)1 : (byte)0);
        w.WriteUInt16(objectId);
        return w.ToArray();
    }

    public static bool TryReadNpcOnOff(ReadOnlySpan<byte> data, out bool on, out ushort objectId)
    {
        on = false;
        objectId = 0;
        if (data.Length < sizeof(byte) + sizeof(ushort)) return false;
        var r = new PacketReader(data);
        on = r.ReadByte() != 0;
        objectId = r.ReadUInt16();
        return true;
    }

    public sealed record NpcOnOffAck(bool On, uint Result, ushort ObjectId, uint Zone, uint ChrType);

    public static byte[] NpcOnOffAckBody(NpcOnOffAck ack)
    {
        ArgumentNullException.ThrowIfNull(ack);
        var w = new PacketWriter(NpcOnOffAckLength);
        w.WriteByte(ack.On ? (byte)1 : (byte)0);
        w.WriteUInt32(ack.Result);
        w.WriteUInt16(ack.ObjectId);
        w.WriteUInt32(ack.Zone);
        w.WriteUInt32(ack.ChrType);
        return w.ToArray();
    }

    public static bool TryReadNpcOnOffAck(ReadOnlySpan<byte> data, out NpcOnOffAck ack)
    {
        ack = null!;
        if (data.Length < NpcOnOffAckLength) return false;
        var r = new PacketReader(data);
        var on = r.ReadByte() != 0;
        var result = r.ReadUInt32();
        var objectId = r.ReadUInt16();
        var zone = r.ReadUInt32();
        var chrType = r.ReadUInt32();
        ack = new NpcOnOffAck(on, result, objectId, zone, chrType);
        return true;
    }

    public static byte[] ZoneId(uint zone) => AckResult(zone);

    public static bool TryReadZoneId(ReadOnlySpan<byte> data, out uint zone) => TryReadAckResult(data, out zone);

    public static byte[] NpcChrType(uint chrType) => AckResult(chrType);

    public static byte[] NpcActivateFailure(bool badIndex, ushort id)
    {
        var w = new PacketWriter(sizeof(byte) + sizeof(ushort));
        w.WriteByte(badIndex ? (byte)1 : (byte)0);
        w.WriteUInt16(id);
        return w.ToArray();
    }

    public static byte[] NpcDeactivateFailure(ushort objectId) => Zone(objectId);

    public sealed record DeactivatedNpc(ushort ObjectId, uint Zone, uint ChrType);

    public static byte[] NpcListBody(IReadOnlyList<DeactivatedNpc> npcs)
    {
        ArgumentNullException.ThrowIfNull(npcs);
        var w = new PacketWriter(sizeof(uint) + npcs.Count * (sizeof(ushort) + 2 * sizeof(uint)));
        w.WriteUInt32((uint)npcs.Count);
        for (var i = 0; i < npcs.Count; i++)
        {
            w.WriteUInt16((ushort)i);
            w.WriteUInt32(npcs[i].Zone);
            w.WriteUInt32(npcs[i].ChrType);
        }
        return w.ToArray();
    }

    public static bool TryReadNpcList(ReadOnlySpan<byte> data, out IReadOnlyList<(ushort Index, uint Zone, uint ChrType)> npcs)
    {
        npcs = Array.Empty<(ushort, uint, uint)>();
        if (data.Length < sizeof(uint)) return false;
        var r = new PacketReader(data);
        var count = r.ReadUInt32();
        var entryLength = sizeof(ushort) + 2 * sizeof(uint);
        if (data.Length - sizeof(uint) < (long)count * entryLength) return false;
        var list = new List<(ushort, uint, uint)>((int)count);
        for (var i = 0; i < count; i++) list.Add((r.ReadUInt16(), r.ReadUInt32(), r.ReadUInt32()));
        npcs = list;
        return true;
    }

    public sealed record CharacterInfo(string Name, ulong CharacterId, ushort Zone, uint Level, long Money, bool Missing, bool Remote);

    public static byte[] CharInfoReport(CharacterInfo info, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(text);
        var buffer = new byte[CharInfoReportLength];
        var name = text.GetBytes(info.Name);
        Array.Copy(name, buffer, Math.Min(name.Length, CharInfoNameLength - 1));
        var w = new PacketWriter(CharInfoReportLength - CharInfoNameLength);
        w.WriteUInt64(info.CharacterId);
        w.WriteUInt16(info.Zone);
        w.WriteUInt32(info.Level);
        w.WriteUInt32Field(info.Money);
        w.WriteByte(info.Remote ? (byte)1 : (byte)0);
        w.WriteByte(info.Missing ? (byte)1 : (byte)0);
        w.ToArray().CopyTo(buffer, CharInfoNameLength);
        return buffer;
    }

    public static bool TryReadCharInfoReport(ReadOnlySpan<byte> data, System.Text.Encoding text, out CharacterInfo info)
    {
        ArgumentNullException.ThrowIfNull(text);
        info = new CharacterInfo(string.Empty, 0, 0, 0, 0, true, false);
        if (data.Length < CharInfoReportLength) return false;
        var nameField = data[..CharInfoNameLength];
        var end = nameField.IndexOf((byte)0);
        var name = text.GetString(end < 0 ? nameField : nameField[..end]);
        var r = new PacketReader(data[CharInfoNameLength..]);
        var characterId = r.ReadUInt64();
        var zone = r.ReadUInt16();
        var level = r.ReadUInt32();
        var money = r.ReadUInt32();
        var remote = r.ReadByte() == 1;
        var missing = r.ReadByte() != 0;
        info = new CharacterInfo(name, characterId, zone, level, money, missing, remote);
        return true;
    }

    public static byte[] CharInfoReplyBody(string account, CharacterInfo info, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(64);
        w.WriteNulTerminated(account, text);
        w.WriteNulTerminated(info.Name, text);
        w.WriteUInt64(info.CharacterId);
        w.WriteUInt16(info.Zone);
        w.WriteUInt32(info.Level);
        w.WriteUInt32Field(info.Money);
        w.WriteByte(info.Remote ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] MegaphoneNoticeRequest(string header, string text, System.Text.Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(header.Length * 2 + text.Length * 2 + 2);
        w.WriteNulTerminated(header, encoding);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static bool TryReadMegaphoneNotice(ReadOnlySpan<byte> data, System.Text.Encoding encoding, out string header, out string text)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        header = string.Empty;
        text = string.Empty;
        var headerEnd = data.IndexOf((byte)0);
        if (headerEnd < 0) return false;
        var rest = data[(headerEnd + 1)..];
        var textEnd = rest.IndexOf((byte)0);
        if (textEnd < 0) return false;
        header = encoding.GetString(data[..headerEnd]);
        text = encoding.GetString(rest[..textEnd]);
        return true;
    }

    public static byte[] MegaphoneNoticeOkBody(uint waiting)
    {
        var body = new byte[sizeof(uint)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(body, waiting);
        return body;
    }

    public static byte[] SuperNoticeRequest(ushort colour, string text, System.Text.Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(sizeof(ushort) + text.Length * 2 + 1);
        w.WriteUInt16(colour);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static bool TryReadSuperNotice(ReadOnlySpan<byte> data, System.Text.Encoding encoding, out ushort colour, out string text)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        colour = 0;
        text = string.Empty;
        if (data.Length < sizeof(ushort) + 1) return false;
        var r = new PacketReader(data);
        colour = r.ReadUInt16();
        var rest = data[sizeof(ushort)..];
        var end = rest.IndexOf((byte)0);
        if (end < 0) return false;
        text = encoding.GetString(rest[..end]);
        return true;
    }

    public static byte[] SuperNoticeShownBody(ushort colour, string text, System.Text.Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(2 * sizeof(ushort) + text.Length * 2 + 1 + sizeof(ushort));
        w.WriteUInt16(0);
        w.WriteUInt16(0);
        w.WriteNulTerminated(text, encoding);
        w.WriteUInt16(colour);
        return w.ToArray();
    }

    public static byte[] CharCountReplyBody(ushort zone, uint count)
    {
        var w = new PacketWriter(sizeof(ushort) + sizeof(uint));
        w.WriteUInt16(zone);
        w.WriteUInt32(count);
        return w.ToArray();
    }
}

public enum SummonAnchor : byte
{
    GameMaster = 0,
    MapArrival = 1,
    Coordinates = 2,
}

public readonly record struct SummonAreaRequest(uint ChrType, byte Count, ushort Radius, SummonAnchor Anchor, ushort Map, ushort X, ushort Y);

public readonly record struct ChatCommandFrame(ulong CharacterId, string Name, string Address, string Line);
