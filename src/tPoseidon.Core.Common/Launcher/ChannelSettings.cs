using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.Common.Launcher;

public sealed record ChannelSettings(string? Name, string? Color, string? Maintenance, string? LauncherVersion)
{
    public static readonly ChannelSettings Empty = new(null, null, null, null);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static ChannelSettings Read(string updatesFolder)
    {
        var path = Path.Combine(updatesFolder, ManifestBuilder.ChannelFile);
        if (!File.Exists(path))
        {
            return Empty;
        }

        try
        {
            return JsonSerializer.Deserialize<ChannelSettings>(File.ReadAllText(path), Options) ?? Empty;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path} is not valid: {ex.Message}", ex);
        }
    }

    public void Write(string updatesFolder)
    {
        Directory.CreateDirectory(updatesFolder);
        var path = Path.Combine(updatesFolder, ManifestBuilder.ChannelFile);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Options));
        File.Move(temporary, path, overwrite: true);
    }
}
