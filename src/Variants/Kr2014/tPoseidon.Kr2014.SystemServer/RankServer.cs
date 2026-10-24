using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Ranking;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class RankServer
{
    private static readonly TimeSpan JobPoll = TimeSpan.FromMinutes(10);

    private readonly RankService _ranks;
    private readonly IRankingStore _store;
    private readonly SocialRoom _room;
    private readonly Action<string> _log;
    private DateTime? _builtWeek;

    public RankServer(RankService ranks, IRankingStore store, SocialRoom room, Action<string> log)
    {
        _ranks = ranks ?? throw new ArgumentNullException(nameof(ranks));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register(RankService.Request, async (_, payload, ct) =>
        {
            if (_ranks.Answer(payload.Span) is not { } reply)
            {
                _log($"[rank] 0x{RankService.Request:X4} [{Convert.ToHexString(payload.Span)}] has no rank object");
                return;
            }
            try
            {
                await ctx.SendFrameAsync(RankService.Reply, reply, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[rank] 0x{RankService.Reply:X4} to {(_room.Find(ctx)?.Character.Name ?? "?")} not delivered: {ex.Message}");
            }
        });
    }

    public async Task RunJobAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(JobPoll);
        do
        {
            var (start, end) = RankingRules.Week(DateTime.Now);
            if (_builtWeek != start)
            {
                try
                {
                    var rows = _store.RebuildMonsterRanks(start, end, RankingRules.TopCount);
                    _builtWeek = start;
                    _log($"[rank] monster quest ranking rebuilt for {start:yyyy-MM-dd}..{end:yyyy-MM-dd}: {rows} rows");
                }
                catch (Exception ex) when (ex is Npgsql.NpgsqlException or InvalidOperationException)
                {
                    _log($"[rank] ranking rebuild failed: {ex.Message}");
                }
            }
        }
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false));
    }
}
