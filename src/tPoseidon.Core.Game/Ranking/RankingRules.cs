namespace tPoseidon.Core.Game.Ranking;

public static class RankingRules
{
    public const byte MonsterQuestRank = 1;
    public const byte CardBattleRank = 2;
    public const int TopCount = 20;
    public const int KillsFirstType = 5;
    public const int TimeFirstType = 6;
    public const byte TimeKind = 0;
    public const byte KillKind = 1;
    public const int CardBattleKinds = 4;
    public const int CardBattleMonster = 0;

    public static int? MonsterRankingType(byte kind) => kind switch
    {
        TimeKind => TimeFirstType,
        KillKind => KillsFirstType,
        _ => null,
    };

    public static int? CardBattleRankingType(byte kind) => kind < CardBattleKinds ? kind + 1 : null;

    public static int MonsterValue(int rankingType, int killCount, int recordTime) =>
        rankingType == TimeFirstType ? recordTime : killCount;

    public static (DateTime Start, DateTime End) Week(DateTime now)
    {
        var daysSinceMonday = ((int)now.DayOfWeek + 6) % 7;
        var monday = now.Date.AddDays(-daysSinceMonday);
        if (now.DayOfWeek == DayOfWeek.Monday) monday = monday.AddDays(-7);
        return (monday, monday.AddDays(7).AddSeconds(-1));
    }
}
