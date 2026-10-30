using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guild;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class GuildRankJob
{
    private static readonly TimeSpan JobPoll = TimeSpan.FromMinutes(10);

    private readonly IGuildStore _guilds;
    private readonly Action<string> _log;

    public GuildRankJob(IGuildStore guilds, Action<string> log)
    {
        _guilds = guilds ?? throw new ArgumentNullException(nameof(guilds));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(JobPoll);
        do
        {
            var weekDay = GuildWeeklyRanking.WeekDayOf(DateTime.Now);
            try
            {
                if (!GuildWeeklyRanking.AlreadyRun(_guilds, weekDay))
                {
                    var ranked = GuildWeeklyRanking.Run(_guilds, weekDay, DateTimeOffset.UtcNow);
                    _log($"[guild] weekly guild ranking for the week of {weekDay}: {ranked} guilds ranked");
                }
            }
            catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
            {
                _log($"[guild] weekly guild ranking failed: {ex.Message}");
            }
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }
}
