using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using tPoseidon.Core.Common.Security;

namespace tPoseidon.Hosting;

public enum PlayerAudience
{
    Local,
    Lan,
    Internet,
}

public sealed class HostSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public int FormatVersion { get; set; } = 1;

    public string ClientFolder { get; set; } = "";

    public string ClientExecutable { get; set; } = "";

    public DatabaseSettings Database { get; set; } = new();

    public NetworkSettings Network { get; set; } = new();

    public PortSettings Ports { get; set; } = new();

    public ClientLaunchSettings Launch { get; set; } = new();

    public bool ShopEnabled { get; set; } = true;

    public string ShopCash { get; set; } = "free";

    public long ShopStartingCash { get; set; }

    public string LastStartedVersion { get; set; } = "";

    public GuardSettings Security { get; set; } = new();

    public static HostSettings? Load(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (!File.Exists(paths.SettingsFile))
        {
            return null;
        }

        var json = File.ReadAllText(paths.SettingsFile);
        HostSettings settings;
        try
        {
            settings = JsonSerializer.Deserialize<HostSettings>(json, JsonOptions)
                ?? throw new HostingException($"{paths.SettingsFile} is empty or not valid settings.");
        }
        catch (JsonException ex)
        {
            throw new HostingException($"{paths.SettingsFile} is not valid settings: {ex.Message}", "Fix the file, or run tposeidon setup again to write a new one.", ex);
        }

        settings.Security ??= new GuardSettings();
        var problems = settings.Security.Problems();
        if (problems.Count > 0)
        {
            throw new HostingException(
                $"{paths.SettingsFile} has security settings that do not work: {string.Join(" ", problems)}",
                "Fix them, or remove the \"security\" section to go back to the defaults.",
                null);
        }

        return settings;
    }

    public static HostSettings LoadRequired(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Load(paths)
            ?? throw new HostingException($"tPoseidon is not set up yet in {paths.Root}. Run 'tposeidon setup' first.");
    }

    public void Save(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Directory.CreateDirectory(paths.Root);
        var json = JsonSerializer.Serialize(this, JsonOptions);
        var temporary = paths.SettingsFile + ".tmp";
        File.WriteAllText(temporary, json);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        File.Move(temporary, paths.SettingsFile, overwrite: true);
    }
}

public sealed class DatabaseSettings
{
    public const string DefaultDatabaseName = "tposeidon";
    public const string DefaultAppLogin = "tposeidon";

    public string Connection { get; set; } = "";

    public string Schema { get; set; } = "kr";

    public string Describe()
    {
        var builder = new NpgsqlConnectionStringBuilder(Connection);
        return $"{builder.Username}@{builder.Host}:{builder.Port}/{builder.Database}";
    }
}

public sealed class NetworkSettings
{
    public PlayerAudience Audience { get; set; } = PlayerAudience.Local;

    public string PublicAddress { get; set; } = "127.0.0.1";

    public string BindAddress => Audience == PlayerAudience.Local ? "127.0.0.1" : "0.0.0.0";
}

public sealed class PortSettings
{
    public int Lobby { get; set; } = 9980;

    public int Master { get; set; } = 16646;

    public int Channel { get; set; } = 10006;

    public int System { get; set; } = 13336;

    public int Game1 { get; set; } = 22006;

    public int Game2 { get; set; } = 23006;

    public int Shop { get; set; } = 9990;
}

public sealed class ClientLaunchSettings
{
    public string ServiceName { get; set; } = "gsp";

    public string Account { get; set; } = "dev";

    public string ServerDescription { get; set; } = "desc";

    public string WorldName { get; set; } = "Trickster";

    public string GroupName { get; set; } = "Trickster";

    public int WorldId { get; set; } = 1;
}
