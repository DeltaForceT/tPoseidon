using System.Text.Json;

namespace tPoseidon.Core.Common;

public sealed class SpawnOverrides
{
    public int GlobalDensityMultiplier { get; set; } = 1;
    public bool ClearAllOnReload { get; set; }
    public bool RespawnAllOnReload { get; set; }

    public static SpawnOverrides Default() => new();

    public static SpawnOverrides LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SpawnOverrides>(json, JsonOpts) ?? new SpawnOverrides();
    }

    public void SaveToFile(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class SpawnOverridesProvider : IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private SpawnOverrides _current;
    private readonly object _lock = new();

    public event Action<SpawnOverrides>? OverridesReloaded;

    public SpawnOverridesProvider(string path, bool watchForChanges = true)
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
            }
        }
    }

    public string FilePath => _path;

    public SpawnOverrides Current
    {
        get { lock (_lock) return _current; }
    }

    public void Reload()
    {
        var fresh = LoadOrDefault();
        SpawnOverrides toNotify;
        lock (_lock)
        {
            _current = fresh;
            toNotify = fresh;
        }
        OverridesReloaded?.Invoke(toNotify);
    }

    public void ClearTransientFlags()
    {
        lock (_lock)
        {
            if (!_current.ClearAllOnReload && !_current.RespawnAllOnReload) return;
            _current.ClearAllOnReload = false;
            _current.RespawnAllOnReload = false;
            try { _current.SaveToFile(_path); }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[spawn] failed to clear transient flags: {ex.Message}");
            }
        }
    }

    private SpawnOverrides LoadOrDefault()
    {
        if (!File.Exists(_path)) return SpawnOverrides.Default();
        try { return SpawnOverrides.LoadFromFile(_path); }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[spawn] failed to load {_path}: {ex.Message} — using defaults");
            return SpawnOverrides.Default();
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
            Console.Error.WriteLine($"[spawn] reload failed: {ex.Message}");
        }
    }

    public void Dispose() => _watcher?.Dispose();
}
