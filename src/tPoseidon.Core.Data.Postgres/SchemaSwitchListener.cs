using Npgsql;

namespace tPoseidon.Core.Data.Postgres;

public sealed class SchemaSwitchListener : IAsyncDisposable
{
        public const string ChannelName = "mt_schema_switch";

    private readonly string _connectionString;
    private readonly Action<string> _logger;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    public SchemaSwitchListener(string connectionString, Action<string>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        _connectionString = connectionString;
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
                _logger($"[postgres] schema-switch listener subscribed to channel '{ChannelName}'");

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
                _logger($"[postgres] schema-switch listener error: {ex.Message}; reconnecting in 5s");
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
            }
        }
    }

    private void OnNotification(object _, NpgsqlNotificationEventArgs args)
    {
        if (!string.Equals(args.Channel, ChannelName, StringComparison.Ordinal)) return;
        var prev = ActiveSchemaProvider.Set(args.Payload);
        var next = ActiveSchemaProvider.Current;
        if (!string.Equals(prev, next, StringComparison.Ordinal))
        {
            _logger($"[postgres] hot schema-switch: {prev} → {next}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        if (_loopTask is not null)
        {
            await _loopTask.ConfigureAwait(false);
        }
        _cts.Dispose();
    }
}
