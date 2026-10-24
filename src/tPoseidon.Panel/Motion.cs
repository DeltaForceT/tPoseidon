using System.Text.Json;

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

public sealed class PanelPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool ReduceAnimations { get; set; }

    public static string FilePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "tPoseidon", "panel.json");

    public static PanelPreferences Load(out string? problem)
    {
        problem = null;
        if (!File.Exists(FilePath))
        {
            return new PanelPreferences();
        }

        try
        {
            return JsonSerializer.Deserialize<PanelPreferences>(File.ReadAllText(FilePath), JsonOptions) ?? new PanelPreferences();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            problem = $"Panel preferences in {FilePath} could not be read ({ex.Message}). Using the defaults.";
            return new PanelPreferences();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temporary, FilePath, overwrite: true);
    }
}
