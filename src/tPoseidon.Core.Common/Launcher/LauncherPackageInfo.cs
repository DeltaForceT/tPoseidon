using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.Common.Launcher;

public sealed record LauncherPackageInfo(string Version, string Executable, bool TestOnThisComputer = false)
{
    public const string FileName = "launcher.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
    };

    public static LauncherPackageInfo? Read(string updatesFolder)
    {
        var path = Path.Combine(updatesFolder, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LauncherPackageInfo>(File.ReadAllText(path), Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{path} is not valid: {ex.Message}", ex);
        }
    }

    public void Write(string updatesFolder)
    {
        Directory.CreateDirectory(updatesFolder);
        var path = Path.Combine(updatesFolder, FileName);
        var temporary = path + ".tmp";
        File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(this, Options));
        File.Move(temporary, path, overwrite: true);
    }

    public static void Remove(string updatesFolder)
    {
        File.Delete(Path.Combine(updatesFolder, FileName));
        var folder = Path.Combine(updatesFolder, ManifestBuilder.LauncherFolder);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
