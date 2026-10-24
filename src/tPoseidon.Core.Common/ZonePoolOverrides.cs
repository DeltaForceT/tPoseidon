using System.Text.Json;

namespace tPoseidon.Core.Common;

public sealed class ZonePoolOverrideEntry
{
    public int MonsterID { get; set; }
    public double Ratio { get; set; } = 1.0;
    public int Avr { get; set; } = 1;
    public int Max { get; set; } = 1;
}

public sealed class ZonePoolOverride
{
    public List<ZonePoolOverrideEntry> Pool { get; set; } = new();
}

public sealed class ZonePoolOverrides
{
    public Dictionary<ushort, ZonePoolOverride> ByMap { get; init; } = new();

    public static ZonePoolOverrides Default() => new();

    public static ZonePoolOverrides LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, ZonePoolOverride>>(json, JsonOpts);
        var dict = new Dictionary<ushort, ZonePoolOverride>(raw?.Count ?? 0);
        if (raw is not null)
        {
            foreach (var (k, v) in raw)
            {
                if (!ushort.TryParse(k, out var id) || v?.Pool is null) continue;
                dict[id] = v;
            }
        }
        return new ZonePoolOverrides { ByMap = dict };
    }

    public ZonePoolOverride? Get(ushort mapId) =>
        ByMap.TryGetValue(mapId, out var e) ? e : null;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class ZonePoolOverridesProvider : IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private ZonePoolOverrides _current;
    private readonly object _lock = new();

    public event Action<ZonePoolOverrides>? OverridesReloaded;

    public ZonePoolOverridesProvider(string path, bool watchForChanges = true)
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

    public ZonePoolOverrides Current
    {
        get { lock (_lock) return _current; }
    }

    public void Reload()
    {
        var fresh = LoadOrDefault();
        ZonePoolOverrides toNotify;
        lock (_lock) { _current = fresh; toNotify = fresh; }
        OverridesReloaded?.Invoke(toNotify);
    }

    private ZonePoolOverrides LoadOrDefault()
    {
        if (!File.Exists(_path)) return ZonePoolOverrides.Default();
        try { return ZonePoolOverrides.LoadFromFile(_path); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[zone-pool-overrides] failed to load {_path}: {ex.Message} — using defaults");
            return ZonePoolOverrides.Default();
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
            Console.Error.WriteLine($"[zone-pool-overrides] reload failed: {ex.Message}");
        }
    }

    public void Dispose() => _watcher?.Dispose();
}
