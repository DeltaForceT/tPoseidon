namespace tPoseidon.Core.Data;

public sealed record GuildLadderEntrant(GuildLadderStanding? Standing, byte Users);

public sealed record GuildLadderSideChange(short GetLadder, int TotalLadder, byte Outcome);

public sealed record GuildLadderSettlement(GuildLadderSideChange Make, GuildLadderSideChange Challenge);

public static class GuildLadderRules
{
    public const byte ResultNone = 0;
    public const byte ResultForfeit = 1;
    public const byte ResultDraw = 2;
    public const byte ResultDecided = 3;

    public const int UnrankedLadder = 1000;
    public const int MasterLadder = 2400;
    public const int SettledAfterMatches = 29;
    public const int NewGuildFactor = 50;
    public const int SettledFactor = 30;
    public const int MasterFactor = 20;
    public const double ExpectationBase = 10.0;
    public const double ExpectationScale = 400.0;
    public const double UsersDivisor = 2.0;
    public const int DailyMatchLimit = 6;
    public const int MinimumLadder = 1;
    public const short FixedStep = 1;

    public const int NotRanked = 0;

    public static byte DisplayedRank(int position) => (byte)Math.Clamp(position, NotRanked, byte.MaxValue);

    public static int Rating(GuildLadderStanding? standing) =>
        standing is { Ladder: > 0 } ? standing.Ladder : UnrankedLadder;

    public static int Factor(GuildLadderStanding? standing)
    {
        if (Rating(standing) >= MasterLadder) return MasterFactor;
        return standing is not null && standing.Decided > SettledAfterMatches ? SettledFactor : NewGuildFactor;
    }

    public static double Expectation(int rating, int opponentRating) =>
        1.0 / (1.0 + Math.Pow(ExpectationBase, (opponentRating - rating) / ExpectationScale));

    public static int WinGain(int winnerRating, int loserRating, int factor, int users) =>
        (int)Math.Truncate(factor * (1.0 - Expectation(winnerRating, loserRating)) + users / UsersDivisor);

    public static int LossDrop(int winnerRating, int loserRating, int factor) =>
        (int)Math.Truncate(factor * Expectation(loserRating, winnerRating));

    public static (byte Make, byte Challenge) Outcomes(byte matchResult, int winTeam) => matchResult switch
    {
        ResultForfeit => (winTeam != 0 ? GuildMatchOutcome.Disconnect : GuildMatchOutcome.Invalid,
            winTeam != 1 ? GuildMatchOutcome.Disconnect : GuildMatchOutcome.Invalid),
        ResultDraw => (GuildMatchOutcome.Draw, GuildMatchOutcome.Draw),
        ResultDecided => (winTeam != 0 ? GuildMatchOutcome.Lose : GuildMatchOutcome.Win,
            winTeam != 1 ? GuildMatchOutcome.Lose : GuildMatchOutcome.Win),
        _ => (GuildMatchOutcome.Invalid, GuildMatchOutcome.Invalid),
    };

    public static GuildLadderSettlement Settle(byte matchResult, int winTeam, bool noLadderChange, bool overDailyLimit, GuildLadderEntrant make, GuildLadderEntrant challenge)
    {
        ArgumentNullException.ThrowIfNull(make);
        ArgumentNullException.ThrowIfNull(challenge);
        var (makeOutcome, challengeOutcome) = Outcomes(matchResult, winTeam);
        var ratings = new[] { Rating(make.Standing), Rating(challenge.Standing) };
        var gets = new int[2];
        var totals = (int[])ratings.Clone();
        if (matchResult == ResultDecided)
        {
            var winner = winTeam == 0 ? 0 : 1;
            var loser = 1 - winner;
            var entrants = new[] { make, challenge };
            if (noLadderChange)
            {
                gets[winner] = FixedStep;
                gets[loser] = -FixedStep;
            }
            else if (!overDailyLimit)
            {
                gets[winner] = WinGain(ratings[winner], ratings[loser], Factor(entrants[winner].Standing), make.Users + challenge.Users);
                totals[winner] = ratings[winner] + gets[winner];
                gets[loser] = -LossDrop(ratings[winner], ratings[loser], Factor(entrants[loser].Standing));
                totals[loser] = ratings[loser] + gets[loser];
            }
            if (totals[loser] < MinimumLadder) totals[loser] = MinimumLadder;
        }
        return new GuildLadderSettlement(
            new GuildLadderSideChange(ToLadderDelta(gets[0]), totals[0], makeOutcome),
            new GuildLadderSideChange(ToLadderDelta(gets[1]), totals[1], challengeOutcome));
    }

    private static short ToLadderDelta(int value) => (short)Math.Clamp(value, short.MinValue, short.MaxValue);
}
