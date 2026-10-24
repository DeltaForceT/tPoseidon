using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.Common;

public sealed class ServerRates
{
    public sealed class GlobalSection
    {
        public double ExpRate { get; init; } = 1.0;
        public double TmxpRate { get; init; } = 1.0;
        public double GalderRate { get; init; } = 1.0;
        public double DropRate { get; init; } = 1.0;
    }

    public sealed class ExpHelperSection
    {
        [JsonPropertyName("EXP_PCRoom")]
        public double ExpPcRoom { get; init; }

        [JsonPropertyName("EXP_Event")]
        public double ExpEvent { get; init; }

        [JsonPropertyName("TM_PCRoom")]
        public double TmPcRoom { get; init; }

        [JsonPropertyName("TM_Event")]
        public double TmEvent { get; init; }
    }

    public sealed class CapsSection
    {
        public ushort MaxLevel { get; init; } = 395;
        public ushort MaxTmLevel { get; init; } = 600;
        public ushort StatPtsPerLevel { get; init; } = 4;
        public ushort MaxStatPtsPerStat { get; init; } = 65535;
        public uint MaxDamagePerHit { get; init; } = 65535;
        public ulong MaxHpPool { get; init; } = uint.MaxValue;
        public ulong MaxMpPool { get; init; } = uint.MaxValue;
        public ulong MaxGalder { get; init; } = uint.MaxValue;
    }

    public sealed class CombatSection
    {
        public float CritMultiplier { get; init; } = 1.5f;
        public float PhysFactor { get; init; } = 0.75f;
        public float MagicFactor { get; init; } = 0.9f;
        public float GunFactor { get; init; } = 30f;
        public float GunAccuracyOffset { get; init; } = 48f;
        public bool MobCanCrit { get; init; }
    }

    public sealed class CadenceSection
    {
        public int PlayerBaseMs { get; init; } = 1900;
        public int MobBaseMs { get; init; } = 1500;
        public int MsPerDxPoint { get; init; } = 20;
        public int MinPlayerMs { get; init; } = 600;
        public int MaxPlayerMs { get; init; } = 3000;
        public int MinMobMs { get; init; } = 400;
        public int MaxMobMs { get; init; } = 4000;
    }

    public GlobalSection Global { get; init; } = new();
    public ExpHelperSection ExpHelper { get; init; } = new();
    public CapsSection Caps { get; init; } = new();
    public CombatSection Combat { get; init; } = new();
    public CadenceSection Cadence { get; init; } = new();

    public int ScaleExp(int baseExp)
    {
        if (baseExp <= 0) return 0;
        var bonus = 1.0 + ExpHelper.ExpPcRoom + ExpHelper.ExpEvent;
        return ClampToInt(baseExp * bonus * Global.ExpRate);
    }

    public int ScaleTmxp(int baseTmxp)
    {
        if (baseTmxp <= 0) return 0;
        var bonus = 1.0 + ExpHelper.TmPcRoom + ExpHelper.TmEvent;
        return ClampToInt(baseTmxp * bonus * Global.TmxpRate);
    }

    public int ScaleGalder(int baseGalder)
    {
        if (baseGalder <= 0) return 0;
        return ClampToInt(baseGalder * Global.GalderRate);
    }

    public double ScaleDropChance(double baseChance)
    {
        if (baseChance <= 0.0) return 0.0;
        var scaled = baseChance * Global.DropRate;
        return scaled > 1.0 ? 1.0 : scaled;
    }

    private static int ClampToInt(double v)
    {
        if (v <= 0) return 0;
        if (v >= int.MaxValue) return int.MaxValue;
        return (int)Math.Round(v);
    }

    public static ServerRates LoadFromFile(string path)
    {
        var json = File.ReadAllText(path);
        var rates = JsonSerializer.Deserialize<ServerRates>(json, JsonOpts)
            ?? throw new InvalidDataException($"Failed to deserialize {path}");
        return rates;
    }

    public static ServerRates Default() => new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string Summary() =>
        $"rates: exp×{Global.ExpRate:0.##} tm×{Global.TmxpRate:0.##} galder×{Global.GalderRate:0.##} drop×{Global.DropRate:0.##}" +
        (HasExpHelperBonuses() ? $" + bonuses[exp_pc={ExpHelper.ExpPcRoom:+0.##;-0.##;0} exp_evt={ExpHelper.ExpEvent:+0.##;-0.##;0} tm_pc={ExpHelper.TmPcRoom:+0.##;-0.##;0} tm_evt={ExpHelper.TmEvent:+0.##;-0.##;0}]" : "") +
        $" caps[lvl={Caps.MaxLevel} tmlvl={Caps.MaxTmLevel} pts/lvl={Caps.StatPtsPerLevel} maxStat={Caps.MaxStatPtsPerStat}]";

    private bool HasExpHelperBonuses() =>
        ExpHelper.ExpPcRoom != 0 || ExpHelper.ExpEvent != 0 ||
        ExpHelper.TmPcRoom != 0 || ExpHelper.TmEvent != 0;
}

public sealed class ServerRatesProvider : IDisposable
{
    private readonly string _path;
    private readonly FileSystemWatcher? _watcher;
    private ServerRates _current;
    private readonly object _lock = new();

    public event Action<ServerRates>? RatesReloaded;

    public ServerRatesProvider(string path, bool watchForChanges = true)
    {
        _path = path;
        _current = LoadOrDefault();

        if (watchForChanges && File.Exists(path))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path))
                ?? throw new InvalidOperationException($"Cannot resolve directory for {path}");
            _watcher = new FileSystemWatcher(dir, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            _watcher.Changed += OnFileChanged;
        }
    }

    public ServerRates Current
    {
        get { lock (_lock) return _current; }
    }

    public void Reload()
    {
        var fresh = LoadOrDefault();
        ServerRates? toNotify = null;
        lock (_lock)
        {
            _current = fresh;
            toNotify = fresh;
        }
        RatesReloaded?.Invoke(toNotify);
    }

    private ServerRates LoadOrDefault()
    {
        if (!File.Exists(_path))
        {
            return ServerRates.Default();
        }
        try
        {
            return ServerRates.LoadFromFile(_path);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[rates] failed to load {_path}: {ex.Message} — falling back to defaults");
            return ServerRates.Default();
        }
    }

    private DateTime _lastReload = DateTime.MinValue;
    private static readonly TimeSpan ReloadDebounce = TimeSpan.FromMilliseconds(500);

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var now = DateTime.UtcNow;
        if (now - _lastReload < ReloadDebounce) return;
        _lastReload = now;
        try
        {
            Reload();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[rates] reload failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _watcher?.Dispose();
    }
}
