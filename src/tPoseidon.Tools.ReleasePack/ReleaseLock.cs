using System.Text.Json;

namespace tPoseidon.Tools.ReleasePack;

internal sealed class ReleaseLock
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public List<RemoteFile> Licenses { get; set; } = new();

    public List<RemoteFile> DrillEggLicenses { get; set; } = new();

    public static ReleaseLock Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "release.lock.json");
        return JsonSerializer.Deserialize<ReleaseLock>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"{path} is empty.");
    }
}

internal sealed class RemoteFile
{
    public string Path { get; set; } = "";

    public Uri? Url { get; set; }

    public string Sha256 { get; set; } = "";
}
