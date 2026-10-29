using System.Text.Json;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;

namespace tPoseidon.Launcher;

public sealed class LauncherSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string ClientFolder { get; set; } = "";

    public List<ServerFile> Servers { get; set; } = [];

    public string SelectedServer { get; set; } = "";

    public string Account { get; set; } = "";

    public string Language { get; set; } = Hosting.ClientFolder.AutomaticService;

    public static string DefaultPath { get; } = PathFor(null);

    public static string PathFor(Branding? branding) =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), branding?.SettingsFolderName ?? "tPoseidon", "launcher.json");

    public static LauncherSettings Load(string path, out string? problem)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        problem = null;
        if (!File.Exists(path))
        {
            return new LauncherSettings();
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(path), JsonOptions) ?? new LauncherSettings();
            loaded.Servers = (loaded.Servers ?? []).Select(server => server.Validated()).ToList();
            return loaded;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or HostingException)
        {
            problem = $"The launcher settings in {path} could not be read ({ex.Message}). Starting with empty settings.";
            return new LauncherSettings();
        }
    }

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions));
        File.Move(temporary, path, overwrite: true);
    }
}
