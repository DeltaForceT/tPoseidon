using System.Text.Json;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientFixLog
{
    public const string FileName = "tposeidon-client-fixes.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<string> CreatedFiles { get; set; } = [];

    public static ClientFixLog Load(string clientFolder)
    {
        var path = Path.Combine(clientFolder, FileName);
        if (!File.Exists(path))
        {
            return new ClientFixLog();
        }

        try
        {
            return JsonSerializer.Deserialize<ClientFixLog>(File.ReadAllText(path), JsonOptions) ?? new ClientFixLog();
        }
        catch (JsonException ex)
        {
            throw new HostingException($"{path} is not valid: {ex.Message}", "Delete the file; it only remembers which files the scanner created.", ex);
        }
    }

    public void Save(string clientFolder)
    {
        var path = Path.Combine(clientFolder, FileName);
        if (CreatedFiles.Count == 0)
        {
            File.Delete(path);
            return;
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }
}
