using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guild;

namespace tPoseidon.Core.Net.World;

public readonly record struct GuildGameFighterMode(ushort EntityId, byte PvpMode);

public readonly record struct GuildResultSide(int Battles, int Wins, int Losses, int Draws, int GetLadder, int TotalLadder)
{
    public static GuildResultSide Before(GuildLeagueRecord league) => new(league.Battles, league.Wins, league.Losses, 0, 0, 0);
}

public readonly record struct GuildScoreSide(uint GuildCode, string GuildName);

public readonly record struct GuildScoreEntry(
    byte Team, ushort CharacterType, ushort Level, string Name, ulong CharacterId, byte State, ushort Kills, ushort Deaths, bool Down);

public static class GuildGameFrames
{
    public const int ResultRowValues = 8;
    public const int ResultTailValues = 4;
    public const ulong TeamKey0 = 0;
    public const ulong TeamKey1 = 1;
    public const int RoomSideCounters = 5;

    public static byte[] Ready(bool sideA, bool sideB) => new[] { sideA ? (byte)1 : (byte)0, sideB ? (byte)1 : (byte)0 };

    public static byte[] GmReady(bool ready) => new[] { ready ? (byte)1 : (byte)0 };

    public static byte[] Countdown(ushort secondsLeft)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(secondsLeft);
        return w.ToArray();
    }

    public static byte[] BattleStart(IReadOnlyList<GuildGameFighterMode> fighters)
    {
        ArgumentNullException.ThrowIfNull(fighters);
        var count = Math.Min(fighters.Count, byte.MaxValue);
        var w = new PacketWriter(1 + count * 8 + 16);
        w.WriteByte((byte)count);
        foreach (var fighter in fighters.Take(count))
        {
            w.WriteUInt16(fighter.EntityId);
            w.WriteUInt32(fighter.PvpMode);
            w.WriteUInt16(0);
        }
        w.WriteUInt64(TeamKey0);
        w.WriteUInt64(TeamKey1);
        return w.ToArray();
    }

    public static byte[] Kill(byte gameType, ulong killerId, ulong victimId, byte respawnFlag)
    {
        var w = new PacketWriter(17);
        w.WriteUInt64(killerId);
        w.WriteUInt64(victimId);
        if (gameType == GuildGameRules.TeamMatch) w.WriteByte(respawnFlag);
        return w.ToArray();
    }

    public static byte[] ScoreBoard(
        uint roomKey, byte gameType, IReadOnlyList<GuildScoreSide> sides, uint secondsLeft, IReadOnlyList<GuildScoreEntry> entries, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(sides);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(256);
        w.WriteUInt32(roomKey);
        w.WriteByte(gameType);
        w.WriteByte(0);
        w.WriteUInt16(0);
        w.WriteUInt16(0);
        w.WriteUInt32(0);
        for (var side = 0; side < GuildGameRules.Teams; side++)
        {
            var known = side < sides.Count;
            w.WriteUInt32(0);
            w.WriteUInt64(known ? sides[side].GuildCode : 0);
            w.WriteNulTerminated(known ? sides[side].GuildName : string.Empty, text);
            w.WriteZero(RoomSideCounters * sizeof(uint));
        }
        w.WriteByte(0);
        w.WriteUInt32(secondsLeft);
        w.WriteUInt16((ushort)Math.Min(entries.Count, ushort.MaxValue));
        foreach (var entry in entries.Take(ushort.MaxValue))
        {
            w.WriteByte(entry.Team);
            w.WriteUInt16(entry.CharacterType);
            w.WriteUInt16(entry.Level);
            w.WriteNulTerminated(entry.Name, text);
            w.WriteUInt64(entry.CharacterId);
            w.WriteByte(entry.State);
            w.WriteUInt16(entry.Kills);
            if (gameType == GuildGameRules.TeamMatch) w.WriteUInt16(entry.Deaths);
            else w.WriteByte(entry.Down ? (byte)1 : (byte)0);
        }
        w.WriteUInt64(TeamKey0);
        w.WriteUInt64(TeamKey1);
        return w.ToArray();
    }

    public static byte[] Result(byte winTeam, byte matchResult, byte reason, GuildResultSide team0, GuildResultSide team1)
    {
        var w = new PacketWriter(3 + (ResultRowValues + ResultTailValues) * 4);
        w.WriteByte(winTeam);
        w.WriteByte(matchResult);
        w.WriteByte(reason);
        foreach (var side in new[] { team0, team1 })
        {
            w.WriteInt32(side.Battles);
            w.WriteInt32(side.Wins);
            w.WriteInt32(side.Losses);
            w.WriteInt32(side.Draws);
        }
        w.WriteInt32(team0.GetLadder);
        w.WriteInt32(team1.GetLadder);
        w.WriteInt32(team0.TotalLadder);
        w.WriteInt32(team1.TotalLadder);
        return w.ToArray();
    }
}
