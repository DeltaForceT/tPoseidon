using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Social;

public sealed record GuildEnterMatch(uint RoomKey, uint GuildCode, uint MapChoice, byte NoLadderFlag, string AcId, byte GameType, string GuildName, IReadOnlyList<ulong> Members, GuildLeagueRecord League, byte ScoreGoal = 0, int Ladder = 0);

public sealed record GuildMatchSideResult(uint GuildCode, byte Count, ushort Kills, ushort Deaths, string Name);

public readonly record struct GuildMatchTerms(byte TimeLimitMinutes, byte ScoreGoal, ushort KillGoal)
{
    public ushort WinningCount(byte gameType) => gameType switch
    {
        GuildGameOrders.TeamMatchType => KillGoal,
        GuildGameOrders.MonsterMatchType => ScoreGoal,
        _ => 0,
    };
}

public sealed record GuildMatchResult(
    uint RoomKey,
    bool NoLadderChange,
    byte GameType,
    GuildMatchTerms Terms,
    ushort MapId,
    DateTimeOffset Begin,
    DateTimeOffset End,
    byte WinTeam,
    byte MatchResult,
    IReadOnlyList<GuildMatchSideResult> Sides);

public sealed record GuildNoticeSide(string GuildName, int Count, byte Rank, ushort AverageLevel)
{
    public static GuildNoticeSide Empty { get; } = new(string.Empty, 0, 0, 0);
}

public static class GuildGameOrders
{
    public const byte TeamMatchType = 0;
    public const byte DeathMatchType = 1;
    public const byte MonsterMatchType = 2;

    public const ushort OrderEnterMatch = 0xE9;
    public const ushort OrderWatchMatch = 0xEA;
    public const ushort OrderResultNotice = 0xEE;
    public const ushort OrderLobbyNotice = 0xF0;
    public const ushort OrderStartNotice = 0xF1;
    public const ushort OrderLadderChange = 0x2CA4;
    public const int LadderSideSize = 6 * sizeof(int);
    public const ushort StartNoticeRequest = 0x2CA5;

    public const ushort MatchResult = 0x15A2;
    public const ushort RoomInvalid = 0x15A4;

    public const ushort LobbyZone = 0xB2;
    public const int ResultSides = 2;
    public const int LeagueRecordSize = 12;
    public const int SystemTimeSize = 16;
    public const int ResultHeaderSize = 4 + 4 + 4 + 4 + 2 + SystemTimeSize + SystemTimeSize + 1 + 1;
    public const int ResultSideMinimum = 4 + 1 + 2 + 2 + 1;

    public static byte[] LadderChange(uint roomKey, GuildResultSide team0, GuildResultSide team1)
    {
        var w = new PacketWriter(sizeof(uint) + 2 * LadderSideSize);
        w.WriteUInt32(roomKey);
        foreach (var side in new[] { team0, team1 })
        {
            w.WriteInt32(side.Battles);
            w.WriteInt32(side.Wins);
            w.WriteInt32(side.Losses);
            w.WriteInt32(side.Draws);
            w.WriteInt32(side.GetLadder);
            w.WriteInt32(side.TotalLadder);
        }
        return w.ToArray();
    }

    public static (uint RoomKey, GuildResultSide Team0, GuildResultSide Team1)? ReadLadderChange(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < sizeof(uint) + 2 * LadderSideSize) return null;
        var r = new PacketReader(payload);
        var roomKey = r.ReadUInt32();
        var sides = new GuildResultSide[ResultSides];
        for (var team = 0; team < ResultSides; team++)
        {
            sides[team] = new GuildResultSide(r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
        }
        return (roomKey, sides[0], sides[1]);
    }

    public static byte[] WatchMatch(uint roomKey)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(roomKey);
        return w.ToArray();
    }

    public static uint? ReadWatchMatch(ReadOnlySpan<byte> data) =>
        data.Length < sizeof(uint) ? null : new PacketReader(data).ReadUInt32();

    public static byte[] EnterMatch(GuildEnterMatch order, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(32 + order.Members.Count * 8);
        w.WriteUInt32(order.RoomKey);
        w.WriteUInt32(order.GuildCode);
        w.WriteUInt32(order.MapChoice);
        w.WriteByte(order.NoLadderFlag);
        w.WriteNulTerminated(order.AcId, text);
        w.WriteByte(order.GameType);
        w.WriteNulTerminated(order.GuildName, text);
        w.WriteByte((byte)Math.Min(order.Members.Count, byte.MaxValue));
        foreach (var member in order.Members.Take(byte.MaxValue)) w.WriteUInt64(member);
        w.WriteInt32(order.Ladder);
        w.WriteInt32(order.League.Battles);
        w.WriteInt32(order.League.Wins);
        w.WriteInt32(order.League.Losses);
        w.WriteByte(order.ScoreGoal);
        return w.ToArray();
    }

    public static GuildEnterMatch? ReadEnterMatch(ReadOnlySpan<byte> data, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (data.Length < 13 || data[13..].IndexOf((byte)0) < 0) return null;
        var r = new PacketReader(data);
        var key = r.ReadUInt32();
        var guild = r.ReadUInt32();
        var map = r.ReadUInt32();
        var noLadderFlag = r.ReadByte();
        var acId = r.ReadNulTerminated(text);
        if (r.Remaining < 3) return null;
        var gameType = r.ReadByte();
        if (data[r.Position..].IndexOf((byte)0) < 0) return null;
        var guildName = r.ReadNulTerminated(text);
        if (r.Remaining < 1) return null;
        var count = r.ReadByte();
        if (r.Remaining < count * 8 + sizeof(int) + LeagueRecordSize) return null;
        var members = new ulong[count];
        for (var index = 0; index < count; index++) members[index] = r.ReadUInt64();
        var ladder = r.ReadInt32();
        var league = new GuildLeagueRecord(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());
        var scoreGoal = r.Remaining >= 1 ? r.ReadByte() : (byte)0;
        return new GuildEnterMatch(key, guild, map, noLadderFlag, acId, gameType, guildName, members, league, scoreGoal, ladder);
    }

    public static byte[] LobbyNotice(byte gameType, uint mapChoice, string guildName, int count, ushort averageLevel, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(32);
        w.WriteByte(gameType);
        w.WriteByte((byte)mapChoice);
        w.WriteNulTerminated(guildName, text);
        w.WriteByte((byte)Math.Clamp(count, 0, byte.MaxValue));
        w.WriteByte(0);
        w.WriteUInt16(averageLevel);
        return w.ToArray();
    }

    public static byte[] StartNotice(byte gameType, uint mapChoice, GuildNoticeSide sideA, GuildNoticeSide sideB, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(sideA);
        ArgumentNullException.ThrowIfNull(sideB);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(64);
        w.WriteByte(gameType);
        w.WriteByte((byte)mapChoice);
        foreach (var side in new[] { sideA, sideB })
        {
            w.WriteNulTerminated(side.GuildName, text);
            w.WriteByte((byte)Math.Clamp(side.Count, 0, byte.MaxValue));
            w.WriteByte(side.Rank);
            w.WriteUInt16(side.AverageLevel);
        }
        return w.ToArray();
    }

    public static bool TryReadStartNoticeRequest(ReadOnlySpan<byte> payload, out byte gameType)
    {
        gameType = 0;
        if (payload.Length < 1) return false;
        gameType = payload[0];
        return true;
    }

    public static byte[] Result(GuildMatchResult result, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(ResultHeaderSize + ResultSides * 32);
        w.WriteUInt32(result.RoomKey);
        w.WriteUInt32(result.NoLadderChange ? 1u : 0u);
        w.WriteUInt32(result.GameType);
        w.WriteByte(result.Terms.TimeLimitMinutes);
        w.WriteByte(result.Terms.ScoreGoal);
        w.WriteUInt16(result.Terms.KillGoal);
        w.WriteUInt16(result.MapId);
        WriteSystemTime(w, result.Begin);
        WriteSystemTime(w, result.End);
        w.WriteByte(result.WinTeam);
        w.WriteByte(result.MatchResult);
        for (var side = 0; side < ResultSides; side++)
        {
            var entry = side < result.Sides.Count ? result.Sides[side] : new GuildMatchSideResult(0, 0, 0, 0, string.Empty);
            w.WriteUInt32(entry.GuildCode);
            w.WriteByte(entry.Count);
            w.WriteUInt16(entry.Kills);
            w.WriteUInt16(entry.Deaths);
            w.WriteNulTerminated(entry.Name, text);
        }
        return w.ToArray();
    }

    public static GuildMatchResult? ReadResult(ReadOnlySpan<byte> data, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (data.Length < ResultHeaderSize) return null;
        var r = new PacketReader(data);
        var key = r.ReadUInt32();
        var noLadderChange = r.ReadUInt32() != 0;
        var gameType = (byte)r.ReadUInt32();
        var terms = new GuildMatchTerms(r.ReadByte(), r.ReadByte(), r.ReadUInt16());
        var map = r.ReadUInt16();
        if (ReadSystemTime(ref r) is not { } begin || ReadSystemTime(ref r) is not { } end) return null;
        var win = r.ReadByte();
        var matchResult = r.ReadByte();
        var sides = new List<GuildMatchSideResult>(ResultSides);
        if (matchResult != 0)
        {
            for (var side = 0; side < ResultSides; side++)
            {
                if (r.Remaining < ResultSideMinimum || data[(r.Position + ResultSideMinimum - 1)..].IndexOf((byte)0) < 0) return null;
                sides.Add(new GuildMatchSideResult(r.ReadUInt32(), r.ReadByte(), r.ReadUInt16(), r.ReadUInt16(), r.ReadNulTerminated(text)));
            }
        }
        return new GuildMatchResult(key, noLadderChange, gameType, terms, map, begin, end, win, matchResult, sides);
    }

    public static void WriteSystemTime(PacketWriter w, DateTimeOffset time)
    {
        ArgumentNullException.ThrowIfNull(w);
        var local = time.ToLocalTime();
        w.WriteUInt16((ushort)local.Year);
        w.WriteUInt16((ushort)local.Month);
        w.WriteUInt16((ushort)local.DayOfWeek);
        w.WriteUInt16((ushort)local.Day);
        w.WriteUInt16((ushort)local.Hour);
        w.WriteUInt16((ushort)local.Minute);
        w.WriteUInt16((ushort)local.Second);
        w.WriteUInt16((ushort)local.Millisecond);
    }

    public static DateTimeOffset? ReadSystemTime(ref PacketReader r)
    {
        if (r.Remaining < SystemTimeSize) return null;
        var year = r.ReadUInt16();
        var month = r.ReadUInt16();
        r.ReadUInt16();
        var day = r.ReadUInt16();
        var hour = r.ReadUInt16();
        var minute = r.ReadUInt16();
        var second = r.ReadUInt16();
        var millisecond = r.ReadUInt16();
        if (year is < 1 or > 9999 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)
            || hour > 23 || minute > 59 || second > 59 || millisecond > 999) return null;
        return new DateTimeOffset(new DateTime(year, month, day, hour, minute, second, millisecond, DateTimeKind.Local));
    }

    public static byte[] ResultNotice(GuildMatchResult result, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(40);
        w.WriteByte(result.WinTeam);
        w.WriteByte(result.MatchResult);
        w.WriteNulTerminated(result.Sides.Count > 0 ? result.Sides[0].Name : string.Empty, text);
        w.WriteNulTerminated(result.Sides.Count > 1 ? result.Sides[1].Name : string.Empty, text);
        return w.ToArray();
    }

    public static byte[] RoomInvalidFor(byte gameType, uint roomKey)
    {
        var w = new PacketWriter(5);
        w.WriteByte(gameType);
        w.WriteUInt32(roomKey);
        return w.ToArray();
    }

    public static (byte GameType, uint RoomKey)? ReadRoomInvalid(ReadOnlySpan<byte> data)
    {
        if (data.Length < 5) return null;
        var r = new PacketReader(data);
        return (r.ReadByte(), r.ReadUInt32());
    }
}
