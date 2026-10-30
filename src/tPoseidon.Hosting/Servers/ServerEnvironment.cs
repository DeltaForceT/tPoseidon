using System.Globalization;
using tPoseidon.Core.Common.Security;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;

namespace tPoseidon.Hosting.Servers;

public static class ServerEnvironment
{
    public static IReadOnlyDictionary<string, string> For(ServerDefinition server, HostSettings settings, InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(paths);
        var inv = CultureInfo.InvariantCulture;
        var clientData = ClientFolder.DataFolder(settings.ClientFolder);
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MT_LOG_FILE"] = paths.LogFile(server.Name),
            ["MT_CRASH_DIR"] = paths.CrashDumps,
            ["MT_DATA_DIR"] = paths.ServerData,
            [PostgresMigrations.MigrationsDirectoryVariable] = paths.Migrations,
            ["MT_VARIANT_DATA_ROOT"] = clientData,
            ["MT_LIBCONFIG_PATH"] = ClientFolder.LibConfigFor(settings),
            ["MT_DB_CONNECTION"] = settings.Database.Connection,
            ["MT_DB_SCHEMA"] = settings.Database.Schema,
            ["MT_PUBLIC_IP"] = settings.Network.PublicAddress,
            ["MT_BIND_ADDRESS"] = settings.Network.BindAddress,
            ["MT_LOBBY_PORT"] = settings.Ports.Lobby.ToString(inv),
            ["MT_LOGIN_PORT"] = settings.Ports.System.ToString(inv),
            ["MT_SYSTEM_PORT"] = settings.Ports.System.ToString(inv),
            ["MT_CHANNEL_PORT"] = settings.Ports.Channel.ToString(inv),
            ["MT_GAME_PORT"] = settings.Ports.Game1.ToString(inv),
            ["MT_GAME_ALT_PORT"] = settings.Ports.Game2.ToString(inv),
            ["MT_MASTER_PORT"] = settings.Ports.Master.ToString(inv),
            [GuildCoinRules.PerWinVariable] = settings.GuildCoinsPerWin.ToString(inv),
        };

        foreach (var pair in Guard(server.Name, settings.Security, paths))
        {
            environment[pair.Key] = pair.Value;
        }

        if (server.IsShop)
        {
            environment["MT_CASH_MODE"] = settings.ShopCash;
            environment["MT_UPDATES_DIR"] = paths.Updates;
            environment["MT_CASH_START"] = settings.ShopStartingCash.ToString(inv);
            environment["MT_WEBSHOP_URLS"] = string.Create(inv, $"http://{settings.Network.BindAddress}:{settings.Ports.Shop}");
        }

        return environment;
    }

    public static IReadOnlyDictionary<string, string> Guard(string serverName, GuardSettings security, InstallPaths paths)
    {
        ArgumentException.ThrowIfNullOrEmpty(serverName);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(paths);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [GuardSettings.SettingsVariable] = security.ToJson(),
            [GuardSettings.StateDirectoryVariable] = paths.Security,
            [GuardSettings.ServerNameVariable] = serverName,
        };
    }

    public static IReadOnlyDictionary<string, string> Guard(string serverName, InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return Guard(serverName, HostSettings.Load(paths)?.Security ?? new GuardSettings(), paths);
    }

    public static IReadOnlyList<string> Arguments(ServerDefinition server, HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(server);
        ArgumentNullException.ThrowIfNull(settings);
        return server.IsShop
            ? Array.Empty<string>()
            : new[] { server.Port(settings.Ports).ToString(CultureInfo.InvariantCulture) };
    }
}
