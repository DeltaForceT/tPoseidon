using System.Text.Json;

namespace tPoseidon.Core.Common;

public sealed class SpawnPointAdded
{
    public int Id { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
}

public sealed class SpawnPointOverride
{
    public List<SpawnPointAdded>? Added { get; set; }
    public List<int>? Removed { get; set; }
}

public sealed class SpawnPointOverrides
{
    public Dictionary<ushort, SpawnPointOverride> ByMap { get; init; } = new();

    public static SpawnPointOverrides Default() => new();

    public static SpawnPointOverrides LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, SpawnPointOverride>>(json, JsonOpts);
        var dict = new Dictionary<ushort, SpawnPointOverride>(raw?.Count ?? 0);
        if (raw is not null)
        {
            foreach (var (k, v) in raw)
            {
                if (!ushort.TryParse(k, out var id) || v is null) continue;
                v.Added ??= new List<SpawnPointAdded>();
                v.Removed ??= new List<int>();
                dict[id] = v;
            }
        }
        return new SpawnPointOverrides { ByMap = dict };
    }

    public SpawnPointOverride? Get(ushort mapId) =>
        ByMap.TryGetValue(mapId, out var e) ? e : null;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class SpawnPointOverridesProvider : IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private SpawnPointOverrides _current;
    private readonly object _lock = new();

    public event Action<SpawnPointOverrides>? OverridesReloaded;

    public SpawnPointOverridesProvider(string path, bool watchForChanges = true)
    {
        _path = path;
        _current = LoadOrDefault();

        if (watchForChanges)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
            {
                _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                    EnableRaisingEvents = true,
                };
                _watcher.Changed += OnFileChanged;
                _watcher.Created += OnFileChanged;
                _watcher.Deleted += OnFileChanged;
            }
        }
    }

    public string FilePath => _path;

    public SpawnPointOverrides Current
    {
        get { lock (_lock) return _current; }
    }

    public void Reload()
    {
        var fresh = LoadOrDefault();
        SpawnPointOverrides toNotify;
        lock (_lock) { _current = fresh; toNotify = fresh; }
        OverridesReloaded?.Invoke(toNotify);
    }

    private SpawnPointOverrides LoadOrDefault()
    {
        if (!File.Exists(_path)) return SpawnPointOverrides.Default();
        try { return SpawnPointOverrides.LoadFromFile(_path); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[spawn-point-overrides] failed to load {_path}: {ex.Message} — using defaults");
            return SpawnPointOverrides.Default();
        }
    }

    private DateTime _lastReload = DateTime.MinValue;
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(500);

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var now = DateTime.UtcNow;
        if (now - _lastReload < ReloadDebounce) return;
        _lastReload = now;
        try { Reload(); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[spawn-point-overrides] reload failed: {ex.Message}");
        }
    }

    public void Dispose() => _watcher?.Dispose();
}
