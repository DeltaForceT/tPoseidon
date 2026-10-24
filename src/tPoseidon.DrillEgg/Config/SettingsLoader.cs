using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.DrillEgg.Config;

internal sealed class SettingsException : Exception
{
    public SettingsException(string message)
        : base(message)
    {
    }

    public SettingsException(string message, Exception inner)
        : base(message, inner)
    {
    }

    public SettingsException()
    {
    }
}

internal sealed record LoadedSettings(DrillEggSettings Settings, string Folder)
{
    public string PathOf(string configured) => Path.GetFullPath(Path.Combine(Folder, configured));

    public string SwfPath => PathOf(Settings.Files.Swf);

    public string ArtFolder => PathOf(Settings.Files.Art);

    public string PicturesFolder => PathOf(Settings.Files.Pictures);

    public string DataFile => PathOf(Settings.Files.Data);
}

internal static class SettingsLoader
{
    public const string DefaultFileName = "drill-egg.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static LoadedSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var full = Path.GetFullPath(path);
        if (!File.Exists(full))
        {
            throw new SettingsException($"The settings file {full} does not exist. Copy one of the files in the examples folder next to the start script and name it {DefaultFileName}.");
        }
        return Parse(File.ReadAllText(full), Path.GetDirectoryName(full)!, full);
    }

    public static LoadedSettings Parse(string json, string folder, string source)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        DrillEggSettings? settings;
        try
        {
            settings = JsonSerializer.Deserialize<DrillEggSettings>(json, Options);
        }
        catch (JsonException e)
        {
            var where = e.LineNumber is { } line ? $" at line {line + 1}" : string.Empty;
            throw new SettingsException($"{source} could not be read{where}: {e.Message}", e);
        }
        if (settings is null) throw new SettingsException($"{source} is empty.");
        var problems = SettingsValidator.Problems(settings);
        if (problems.Count > 0)
        {
            throw new SettingsException($"{source} has {problems.Count} problem(s):{Environment.NewLine}  - {string.Join(Environment.NewLine + "  - ", problems)}");
        }
        return new LoadedSettings(settings, Path.GetFullPath(folder));
    }
}
