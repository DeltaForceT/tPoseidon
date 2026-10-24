using System.Globalization;
using Npgsql;
using tPoseidon.Hosting.Processes;

namespace tPoseidon.Hosting.Database;

public static class DatabaseBackup
{
    private static readonly string[] PostgresAppBin = { "/Applications/Postgres.app/Contents/Versions/latest/bin" };

    public static string LocatePgDump() =>
        FindPgDump()
        ?? throw new HostingException(
            "pg_dump was not found.",
            "It comes with PostgreSQL: add PostgreSQL's bin folder to PATH, or install the PostgreSQL client tools (see docs/install/postgresql.md).",
            null);

    public static string RestoreHint(string backupFile) =>
        "Restore it into an empty database with: pg_restore --clean --if-exists -d <database> " + Path.GetFileName(backupFile);

    public static string Run(InstallPaths paths, HostSettings settings, string pgDump)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(settings);
        Directory.CreateDirectory(paths.Backups);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var target = Path.Combine(paths.Backups, $"tposeidon-{stamp}.dump");
        var connection = new NpgsqlConnectionStringBuilder(settings.Database.Connection);
        var arguments = new List<string>
        {
            "--format=custom",
            "--file=" + target,
            "--host=" + connection.Host,
            "--port=" + connection.Port.ToString(CultureInfo.InvariantCulture),
            "--username=" + connection.Username,
            "--no-password",
            connection.Database ?? throw new HostingException("The saved connection has no database name. Run tposeidon setup again."),
        };
        var environment = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(connection.Password))
        {
            environment["PGPASSWORD"] = connection.Password;
        }

        var result = ToolRunner.Capture(pgDump, arguments, environment);
        if (result.ExitCode != 0)
        {
            throw new HostingException($"pg_dump failed (exit {result.ExitCode}).", result.Output, null);
        }

        return target;
    }

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
