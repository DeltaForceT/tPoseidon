using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.PvP;

public static class PvpDuelLedger
{
    public const byte MatchType = 0;
    public const byte ResultWin = 1;
    public const byte ResultLose = 2;
    public const byte ResultDraw = 3;
    public const byte ResultDisconnect = 4;

    public static void Save(IPvpStore store, PvpDuelOutcome outcome, DateTimeOffset begin, DateTimeOffset end)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(outcome);
        if (outcome.Cancelled) return;
        for (var seat = 0; seat < outcome.Seats.Count; seat++)
        {
            var result = outcome.Seats[seat];
            if (result.ResultCode == 0) continue;
            var other = outcome.Seats[PvpDuel.Other(seat)];
            var ladder = store.GetOrCreateLadder(result.CharacterId);
            store.UpsertLadder(ladder with
            {
                Ladder = ladder.Ladder + result.LadderGain,
                MatchCount = Bump(ladder.MatchCount, 1),
                WinCount = Bump(ladder.WinCount, result.ResultCode == ResultWin ? 1 : 0),
                LoseCount = Bump(ladder.LoseCount, result.ResultCode == ResultLose ? 1 : 0),
                DrawCount = Bump(ladder.DrawCount, result.ResultCode == ResultDraw ? 1 : 0),
                DiscCount = Bump(ladder.DiscCount, result.ResultCode == ResultDisconnect ? 1 : 0),
            });
            store.RecordMatch(new PvpMatch
            {
                MatchBegin = begin,
                MatchEnd = end,
                MatchType = MatchType,
                MatchMap = (short)result.ZoneId,
                MatchTimeout = PvpDuel.TimeoutMinutes,
                JoinType = (byte)seat,
                CharacterId = result.CharacterId,
                OpponentCharacterId = other.CharacterId,
                GetLadder = result.LadderGain,
                GetExp = (int)result.Exp,
                MatchResult = (byte)result.ResultCode,
            });
        }
    }

    private static short Bump(short value, int by) => (short)Math.Min(value + by, short.MaxValue);
}
