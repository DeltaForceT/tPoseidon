namespace tPoseidon.Hosting;

public sealed class InstallPaths
{
    public const string HomeVariable = "TPOSEIDON_HOME";
    private const string AppFolderName = "app";

    public InstallPaths(string root)
    {
        Root = Path.GetFullPath(root);
    }

    public string Root { get; }

    public string App => Path.Combine(Root, AppFolderName);

    public string Pgsql => Path.Combine(Root, "pgsql");

    public string PgsqlBin => Path.Combine(Pgsql, "bin");

    public string PgsqlLib => Path.Combine(Pgsql, "lib");

    public string Database => Path.Combine(Root, "db");

    public string Logs => Path.Combine(Root, "log");

    public string CrashDumps => Path.Combine(Logs, "dmp");

    public string PostgresLog => Path.Combine(Logs, "postgres.log");

    public string Run => Path.Combine(Root, "run");

    public string Security => Path.Combine(Run, "security");

    public string Backups => Path.Combine(Root, "backups");

    public string Updates => Path.Combine(Root, "updates");

    public string LauncherHistory => Path.Combine(Root, "launcher-history.json");

    public string ServerData => Path.Combine(Root, "server-data");

    public string KeyTable => Path.Combine(ServerData, "keytable-s2v2010.bin");

    public string Migrations => Path.Combine(App, "migrations");

    public string SettingsFile => Path.Combine(Root, "tposeidon.json");

    public string AppExecutable(string assemblyName) => Path.Combine(App, Executable(assemblyName));

    public string PgsqlTool(string name) => Path.Combine(PgsqlBin, Executable(name));

    public string LogFile(string serviceName) => Path.Combine(Logs, serviceName + ".log");

    public string PidFile(string serviceName) => Path.Combine(Run, serviceName + ".pid");

    public static string Executable(string name) => OperatingSystem.IsWindows() ? name + ".exe" : name;

    public static InstallPaths Resolve(string? explicitRoot)
    {
        if (!string.IsNullOrWhiteSpace(explicitRoot))
        {
            return new InstallPaths(explicitRoot.Trim());
        }

        var fromEnvironment = Environment.GetEnvironmentVariable(HomeVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return new InstallPaths(fromEnvironment.Trim());
        }

        return FromBaseDirectory(AppContext.BaseDirectory);
    }

    public static InstallPaths ResolveOrDefault(string defaultRoot)
    {
        ArgumentException.ThrowIfNullOrEmpty(defaultRoot);
        var fromEnvironment = Environment.GetEnvironmentVariable(HomeVariable);
        return new InstallPaths(string.IsNullOrWhiteSpace(fromEnvironment) ? defaultRoot : fromEnvironment.Trim());
    }

    public static InstallPaths FromBaseDirectory(string baseDirectory)
    {
        var directory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(baseDirectory));
        var insideAppFolder = string.Equals(directory.Name, AppFolderName, StringComparison.OrdinalIgnoreCase)
            && directory.Parent is not null;
        return new InstallPaths(insideAppFolder ? directory.Parent!.FullName : directory.FullName);
    }
}
