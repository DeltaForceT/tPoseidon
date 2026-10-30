using System.Globalization;
using System.Net.NetworkInformation;
using Npgsql;
using tPoseidon.Hosting.Processes;

namespace tPoseidon.Hosting.Database;

public static class DatabaseBackup
{
    private static readonly string[] PostgresAppBin = { "/Applications/Postgres.app/Contents/Versions/latest/bin" };

    public static BackupTool Locate(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var target = TargetOf(settings);
        var choice = BackupToolChooser.Choose(FindPgDump(), () => FindContainer(target));
        return choice.Tool
            ?? throw new HostingException(
                "No way to back up the database was found.",
                BackupToolChooser.NoToolHint(target.Host, target.Port, choice.Docker),
                null);
    }

    public static string RestoreHint(string backupFile) =>
        "Restore it into an empty database with: pg_restore --clean --if-exists -d <database> " + Path.GetFileName(backupFile);

    public static string Run(InstallPaths paths, HostSettings settings, BackupTool tool)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(tool);
        Directory.CreateDirectory(paths.Backups);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(paths.Backups, $"tposeidon-{stamp}.dump");
        var source = TargetOf(settings);
        var environment = BackupArguments.Environment(source);
        var result = tool.Container is { } endpoint
            ? Stream(tool.Executable, BackupArguments.Container(source, endpoint), environment, target)
            : ToolRunner.Capture(tool.Executable, BackupArguments.Local(source, target), environment);
        if (result.ExitCode != 0)
        {
            throw new HostingException($"pg_dump failed (exit {result.ExitCode}).", result.Output, null);
        }

        SigningKeyStore.BackupTo(paths, target);
        return target;
    }

    private static ToolResult Stream(string executable, IReadOnlyList<string> arguments, IReadOnlyDictionary<string, string> environment, string file)
    {
        ToolResult result;
        using (var output = File.Create(file))
        {
            result = ToolRunner.CaptureTo(executable, arguments, environment, output);
        }

        if (result.ExitCode != 0)
        {
            File.Delete(file);
        }

        return result;
    }

    private static BackupTarget TargetOf(HostSettings settings)
    {
        var connection = new NpgsqlConnectionStringBuilder(settings.Database.Connection);
        return new BackupTarget(
            connection.Host ?? "",
            connection.Port,
            connection.Username ?? "",
            connection.Database ?? throw new HostingException("The saved connection has no database name. Run tposeidon setup again."),
            connection.Password);
    }

    private static DockerLookup FindContainer(BackupTarget target)
    {
        var docker = ToolRunner.FindOnPath("docker");
        if (docker is null)
        {
            return DockerLookup.NotInstalled;
        }

        ToolResult listing;
        try
        {
            listing = ToolRunner.Capture(docker, ["ps", "--format", DockerPorts.PsFormat]);
        }
        catch (HostingException ex)
        {
            return DockerLookup.Unavailable(docker, ex.Summary);
        }

        if (listing.ExitCode != 0)
        {
            return DockerLookup.Unavailable(docker, listing.Output);
        }

        return DockerPorts.Find(listing.Output, target.Host, target.Port, LocalAddresses()) is { } endpoint
            ? DockerLookup.Match(docker, endpoint)
            : DockerLookup.NoMatch(docker);
    }

    private static List<string> LocalAddresses() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address.ToString())
            .ToList();

    public static string? FindPgDump()
    {
        var onPath = ToolRunner.FindOnPath("pg_dump");
        if (onPath is not null)
        {
            return onPath;
        }

        return Candidates()
            .Select(directory => Path.Combine(directory, InstallPaths.Executable("pg_dump")))
            .Where(File.Exists)
            .OrderByDescending(MajorVersionOf)
            .FirstOrDefault();
    }

    private static IEnumerable<string> Candidates()
    {
        if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            return Subdirectories(Path.Combine(programFiles, "PostgreSQL")).Select(version => Path.Combine(version, "bin"));
        }

        if (OperatingSystem.IsMacOS())
        {
            return PostgresAppBin
                .Concat(Subdirectories("/opt/homebrew/opt").Where(IsPostgresFormula).Select(formula => Path.Combine(formula, "bin")))
                .Concat(Subdirectories("/usr/local/opt").Where(IsPostgresFormula).Select(formula => Path.Combine(formula, "bin")));
        }

        return Subdirectories("/usr/lib/postgresql").Select(version => Path.Combine(version, "bin"))
            .Concat(Directory.Exists("/usr") ? Directory.EnumerateDirectories("/usr", "pgsql-*").Select(version => Path.Combine(version, "bin")) : Enumerable.Empty<string>());
    }

    private static bool IsPostgresFormula(string directory) =>
        Path.GetFileName(directory).StartsWith("postgresql", StringComparison.Ordinal);

    private static IEnumerable<string> Subdirectories(string directory) =>
        Directory.Exists(directory) ? Directory.EnumerateDirectories(directory) : Enumerable.Empty<string>();

    private static int MajorVersionOf(string pgDump)
    {
        var versionFolder = Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(pgDump))) ?? "";
        var digits = new string(versionFolder.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var version) ? version : 0;
    }
}
