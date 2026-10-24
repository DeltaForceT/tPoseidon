using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class WorldSnapshotReloadListener : IAsyncDisposable
{
    public const string ChannelName = "mt_world_snapshot_reload";

    private readonly string _connectionString;
    private readonly Action<string> _onReload;
    private readonly Action<string> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    public WorldSnapshotReloadListener(string connectionString, Action<string> onReload, Action<string>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        ArgumentNullException.ThrowIfNull(onReload);
        _connectionString = connectionString;
        _onReload = onReload;
        _logger = logger ?? Console.WriteLine;
    }

    public void Start()
    {
        if (_loopTask is not null) return;
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(_connectionString);
                await conn.OpenAsync(ct).ConfigureAwait(false);
                conn.Notification += OnNotification;

                await using (var listenCmd = new NpgsqlCommand($"LISTEN {ChannelName}", conn))
                {
                    await listenCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
                _logger($"[postgres] world-snapshot-reload listener subscribed to channel '{ChannelName}'");

                while (!ct.IsCancellationRequested)
                {
                    await conn.WaitAsync(ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger($"[postgres] world-snapshot-reload listener error: {ex.Message}; reconnecting in 5s");
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private void OnNotification(object _, NpgsqlNotificationEventArgs args)
    {
        if (!string.Equals(args.Channel, ChannelName, StringComparison.Ordinal)) return;
        try
        {
            _onReload(args.Payload ?? string.Empty);
        }
        catch (Exception ex)
        {
            _logger($"[postgres] snapshot reload handler threw: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loopTask is not null)
        {
            try { await _loopTask.ConfigureAwait(false); } catch {  }
        }
        _cts.Dispose();
    }
}
