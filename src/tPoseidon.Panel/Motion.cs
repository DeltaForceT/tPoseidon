using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;

namespace tPoseidon.Panel;

public sealed record MotionState(bool Ambient, bool Events);

public sealed class Motion
{
    public static readonly Motion Shared = new();

    private bool _focused = true;
    private bool _minimized;
    private bool _reduced;

    public event Action? Changed;

    public bool Ambient => !_reduced && !_minimized && _focused;

    public bool Events => !_reduced && !_minimized;

    public bool Reduced => _reduced;

    public MotionState State => new(Ambient, Events);

    public void SetFocused(bool value) => Set(ref _focused, value);

    public void SetMinimized(bool value) => Set(ref _minimized, value);

    public void SetReduced(bool value) => Set(ref _reduced, value);

    private void Set(ref bool field, bool value)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        Changed?.Invoke();
    }
}

public enum BubbleVisibility
{
    WhenHidden,
    Always,
    Off,
}

public sealed class PanelPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool ReduceAnimations { get; set; }

    public const int BubbleLayoutCurrent = 2;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BubbleVisibility? Bubble { get; set; }

    public bool BubbleEnabled { get; set; } = true;

    public int BubbleLayoutVersion { get; set; }

    public int? BubbleX { get; set; }

    public int? BubbleY { get; set; }

    [JsonIgnore]
    public PixelPoint? SavedBubble =>
        BubbleLayoutVersion == BubbleLayoutCurrent && BubbleX is { } x && BubbleY is { } y ? new PixelPoint(x, y) : null;

    public void RememberBubble(PixelPoint position)
    {
        BubbleX = position.X;
        BubbleY = position.Y;
        BubbleLayoutVersion = BubbleLayoutCurrent;
    }

    public void MigrateLegacy()
    {
        if (Bubble is { } legacy)
        {
            BubbleEnabled = legacy != BubbleVisibility.Off;
            Bubble = null;
        }
    }

    public static string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tPoseidon", "panel.json");

    public static PanelPreferences Load(out string? problem) => Load(FilePath, out problem);

    public static PanelPreferences Load(string path, out string? problem)
    {
        problem = null;
        if (!File.Exists(path))
        {
            return new PanelPreferences();
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<PanelPreferences>(File.ReadAllText(path), JsonOptions) ?? new PanelPreferences();
            loaded.MigrateLegacy();
            return loaded;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            problem = $"Panel preferences in {path} could not be read ({ex.Message}). Using the defaults.";
            return new PanelPreferences();
        }
    }

    [JsonIgnore]
    public string? StorePath { get; set; }

    public void Save() => Save(StorePath ?? FilePath);

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }

    public bool TrySave()
    {
        try
        {
            Save();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogHub.Write(LogHub.PanelSource, LogLevel.Warn, $"Could not save the panel preferences to {StorePath ?? FilePath}: {ex.Message}");
            return false;
        }
    }
}
