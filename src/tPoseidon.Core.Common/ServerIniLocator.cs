namespace tPoseidon.Core.Common;

public static class ServerIniLocator
{
    public enum ServerKind
    {
        Master,
        Login,
        System,
        Game,
    }

    public enum Variant
    {
        Kr2014,
    }

    public static string Resolve(ServerKind kind, Variant variant, int instance = 1)
    {
        var data = RepoRoot.DataDir;
        return kind switch
        {
            ServerKind.Master =>
                System.IO.Path.Combine(data, VariantFolder(variant), "MasterServer.ini"),
            ServerKind.Login =>
                System.IO.Path.Combine(data, VariantFolder(variant), "LoginServer", "LoginServer.ini"),
            ServerKind.System =>
                System.IO.Path.Combine(data, VariantFolder(variant), "SystemServer", "SystemServer.ini"),
            ServerKind.Game =>
                System.IO.Path.Combine(data, VariantFolder(variant), $"GameServer{instance}", "GameServer.ini"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static string VariantFolder(Variant variant) => variant switch
    {
        Variant.Kr2014 => "Kr2014",
        _ => throw new ArgumentException($"unknown variant {variant}", nameof(variant)),
    };

    public static IniFile? TryLoad(ServerKind kind, Variant variant, int instance, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        var path = Resolve(kind, variant, instance);
        if (!System.IO.File.Exists(path))
        {
            log($"{kind} INI not found at {path}; using compile-time defaults");
            return null;
        }
        try
        {
            var ini = IniFile.LoadFromFile(path);
            log($"{kind} INI loaded from {path}");
            return ini;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log($"{kind} INI load FAILED ({ex.GetType().Name}: {ex.Message}); using compile-time defaults");
            return null;
        }
    }
}
