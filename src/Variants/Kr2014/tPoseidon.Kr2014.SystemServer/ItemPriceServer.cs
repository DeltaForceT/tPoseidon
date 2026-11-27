using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Trident;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class ItemPriceServer
{
    public const string DefaultFileName = "TDB_ITEMPRICE.log";
    public const int SaveHour = 4;
    public static readonly TimeSpan SavePeriod = TimeSpan.FromHours(1);

    private readonly ItemPriceBoard _board = new();
    private readonly string _path;
    private readonly Func<DateTime> _now;
    private readonly Action<string> _log;

    public ItemPriceServer(string path, Func<DateTime> now, Action<string> log)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public ItemPriceBoard Board => _board;

    public int Load()
    {
        if (!File.Exists(_path)) return 0;
        using var reader = new StreamReader(_path);
        var loaded = _board.Load(reader);
        _log($"[itemprice] {loaded} item prices loaded from {_path}");
        return loaded;
    }

    public int Save()
    {
        var staging = _path + ".tmp";
        int saved;
        using (var writer = new StreamWriter(staging, false))
        {
            saved = _board.Save(writer);
        }
        File.Move(staging, _path, true);
        _log($"[itemprice] {saved} item prices saved to {_path}");
        return saved;
    }

    public bool SaveIfDue()
    {
        if (_now().Hour != SaveHour) return false;
        Save();
        return true;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(SavePeriod);
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false)) SaveIfDue();
    }

    public void Register(OpcodeDispatcher dispatcher, TridentSessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register(ItemPriceFrames.Report, (_, payload, _) =>
        {
            if (ItemPriceFrames.TryReadReport(payload.Span, out var itemId, out var price)) _board.Record(itemId, price);
            return Task.CompletedTask;
        });
        dispatcher.Register(ItemPriceFrames.Query, (_, payload, ct) =>
            ItemPriceFrames.TryReadQuery(payload.Span, out var itemId)
                ? ctx.SendFrameAsync(ItemPriceFrames.Statistics, ItemPriceFrames.Reply(itemId, _board.Find(itemId)), (byte)Random.Shared.Next(1, 256), ct)
                : Task.CompletedTask);
    }
}
