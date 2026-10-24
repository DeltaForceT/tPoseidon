using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.Common;

public sealed class MonsterOverrideEntry
{
    public string? Name { get; set; }
    public int? Level { get; set; }
    public int? MaxHpLv { get; set; }
    public int? MaxMpLv { get; set; }
    public int? ApLv { get; set; }
    public int? AcLv { get; set; }
    public int? DpLv { get; set; }
    public int? MdLv { get; set; }
    public int? Exp { get; set; }
    public int? Tm { get; set; }
    public int? GMin { get; set; }
    public int? GMax { get; set; }
    public int? ItemDropIdx { get; set; }
    public bool? IsBoss { get; set; }
}

public sealed class MonsterOverrides
{
    public Dictionary<int, MonsterOverrideEntry> ById { get; init; } = new();

    public static MonsterOverrides Default() => new();

    public static MonsterOverrides LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, MonsterOverrideEntry>>(json, JsonOpts);
        var dict = new Dictionary<int, MonsterOverrideEntry>(raw?.Count ?? 0);
        if (raw is not null)
        {
            foreach (var (k, v) in raw)
            {
                if (!int.TryParse(k, out var id) || v is null) continue;
                dict[id] = v;
            }
        }
        return new MonsterOverrides { ById = dict };
    }

    public MonsterOverrideEntry? Get(int monsterId) =>
        ById.TryGetValue(monsterId, out var e) ? e : null;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}

public sealed class MonsterOverridesProvider : IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private MonsterOverrides _current;
    private readonly object _lock = new();

    public event Action<MonsterOverrides>? OverridesReloaded;

    public MonsterOverridesProvider(string path, bool watchForChanges = true)
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

    public MonsterOverrides Current
    {
        get { lock (_lock) return _current; }
    }

    public void Reload()
    {
        var fresh = LoadOrDefault();
        MonsterOverrides toNotify;
        lock (_lock) { _current = fresh; toNotify = fresh; }
        OverridesReloaded?.Invoke(toNotify);
    }

    private MonsterOverrides LoadOrDefault()
    {
        if (!File.Exists(_path)) return MonsterOverrides.Default();
        try { return MonsterOverrides.LoadFromFile(_path); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[monster-overrides] failed to load {_path}: {ex.Message} — using defaults");
            return MonsterOverrides.Default();
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
            Console.Error.WriteLine($"[monster-overrides] reload failed: {ex.Message}");
        }
    }

    public void Dispose() => _watcher?.Dispose();
}
