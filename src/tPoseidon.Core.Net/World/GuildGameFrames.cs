using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.World;

public readonly record struct GuildGameFighterMode(ushort EntityId, byte PvpMode);

public readonly record struct GuildResultSide(int Battles, int Wins, int Losses, int Draws, int GetLadder, int TotalLadder)
{
    public static GuildResultSide Before(GuildLeagueRecord league) => new(league.Battles, league.Wins, league.Losses, 0, 0, 0);
}

public static class GuildGameFrames
{
    public const int ResultRowValues = 8;
    public const int ResultTailValues = 4;
    public const ulong TeamKey0 = 0;
    public const ulong TeamKey1 = 1;

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
