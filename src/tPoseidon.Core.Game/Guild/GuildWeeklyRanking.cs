using tPoseidon.Core.Data;
using GuildRecord = tPoseidon.Core.Data.Guild;

namespace tPoseidon.Core.Game.Guild;

public static class GuildWeeklyRanking
{
    public const int PageSize = 200;
    public const string WeekDayFormat = "yyyy-MM-dd";

    public const int DaysPerWeek = 7;

    public static string WeekDayOf(DateTime now)
    {
        var sinceMonday = ((int)now.DayOfWeek + DaysPerWeek - 1) % DaysPerWeek;
        return now.Date.AddDays(-sinceMonday).ToString(WeekDayFormat, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static bool AlreadyRun(IGuildStore store, string weekDay) => store.GetRanking(weekDay, 1).Count > 0;

    public static int Run(IGuildStore store, string weekDay, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(store);
        var ranked = AllGuilds(store)
            .Select(guild => (Guild: guild, Ladder: store.GetLadder(guild.GuildCode)))
            .Where(entry => entry.Ladder is not null)
            .Select(entry => (entry.Guild, Ladder: Math.Max(entry.Ladder!.LadderScore, 0)))
            .OrderByDescending(entry => Math.Max(entry.Guild.GuildPointIncrease, 0))
            .ThenByDescending(entry => entry.Ladder)
            .ThenByDescending(entry => entry.Guild.GuildMembers)
            .ThenByDescending(entry => entry.Guild.GuildCreatedAt)
            .ThenBy(entry => entry.Guild.GuildCode)
            .ToList();
        var rank = 0;
        foreach (var (guild, _) in ranked)
        {
            rank++;
            var weekPoints = Math.Max(guild.GuildPointIncrease, 0);
            store.UpdateGuildHeader(guild with { GuildRanking = rank, GuildRankingPoint = weekPoints, GuildPointIncrease = 0 });
            store.SnapshotRanking(new GuildRankingEntry
            {
                WeekDay = weekDay,
                Ranking = rank,
                GuildCode = guild.GuildCode,
                GuildName = guild.GuildName,
                GuildPointIncrease = weekPoints,
                GuildRankingPoint = Math.Max(guild.GuildRankingPoint, 0),
                SnapshotAt = now,
            });
        }
        return ranked.Count;
    }

    public static IReadOnlyList<GuildRecord> AllGuilds(IGuildStore store)
    {
        var all = new List<GuildRecord>();
        while (true)
        {
            var page = store.ListGuilds(all.Count, PageSize);
            all.AddRange(page);
            if (page.Count < PageSize) return all;
        }
    }
}
