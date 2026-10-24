using Avalonia.Threading;
using tPoseidon.Hosting.Logs;

namespace tPoseidon.Panel;

public enum LogLevel
{
    Info,
    Warn,
    Error,
}

public sealed record LogLine(DateTime Time, string Server, LogLevel Level, string Message);

public sealed record LogSource(string Server, string Path);

public static class LogHub
{
    public const string PanelSource = "Panel";
    private const int Capacity = 2000;
    private const int HistoryBytes = 16 * 1024;
    private static readonly TimeSpan PumpInterval = TimeSpan.FromMilliseconds(400);
    private static readonly List<LogLine> LinesStore = [];
    private static readonly PersonalPaths Personal = new();
    private static readonly HashSet<string> ReportedFailures = new(StringComparer.Ordinal);
    private static CancellationTokenSource? s_pump;

    public static event Action<LogLine>? Added;

    public static IReadOnlyList<LogLine> Lines => LinesStore;

    public static void Write(string server, LogLevel level, string message)
    {
        Dispatcher.UIThread.VerifyAccess();
        var line = new LogLine(DateTime.Now, server, level, message);
        LinesStore.Add(line);
        if (LinesStore.Count > Capacity)
        {
            LinesStore.RemoveRange(0, LinesStore.Count - Capacity);
        }

        Added?.Invoke(line);
    }

    public static void Post(string server, LogLevel level, string message) =>
        Dispatcher.UIThread.Post(() => Write(server, level, message));

    public static void Follow(IReadOnlyList<LogSource> sources)
    {
        s_pump?.Cancel();
        s_pump = new CancellationTokenSource();
        var token = s_pump.Token;
        var followers = sources.Select(source => (source.Server, Follower: new LogFollower(source.Path, HistoryBytes))).ToList();
        _ = Task.Run(() => PumpAsync(followers, token), token);
    }

    public static void StopFollowing()
    {
        s_pump?.Cancel();
        s_pump = null;
    }

    public static string HidePaths(string text) => Personal.Hide(text);

    public static LogLevel LevelOf(string text) => LogClassifier.Classify(text) switch
    {
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Warning => LogLevel.Warn,
        _ => LogLevel.Info,
    };

    private static async Task PumpAsync(List<(string Server, LogFollower Follower)> followers, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(PumpInterval);
        do
        {
            var batch = new List<(string Server, string Text)>();
            foreach (var (server, follower) in followers)
            {
                try
                {
                    batch.AddRange(follower.ReadNew().Select(text => (server, text)));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    var key = follower.Path;
                    var message = $"Cannot read {follower.Path}: {ex.Message}";
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (ReportedFailures.Add(key))
                        {
                            Write(PanelSource, LogLevel.Warn, message);
                        }
                    });
                }
            }

            if (batch.Count > 0)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    foreach (var (server, text) in batch)
                    {
                        Write(server, LevelOf(text), text);
                    }
                });
            }
        }
        while (await NextAsync(timer, ct).ConfigureAwait(false));
    }

    private static async Task<bool> NextAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
