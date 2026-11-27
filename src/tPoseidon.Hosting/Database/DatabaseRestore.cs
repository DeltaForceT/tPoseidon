using System.Globalization;
using Npgsql;
using tPoseidon.Hosting.Processes;

namespace tPoseidon.Hosting.Database;

public static class RestoreArguments
{
    public static IReadOnlyList<string> Local(BackupTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return
        [
            "--no-owner",
            "--host=" + target.Host,
            "--port=" + target.Port.ToString(CultureInfo.InvariantCulture),
            "--username=" + target.User,
            "--no-password",
            "--dbname=" + target.Database,
        ];
    }

    public static IReadOnlyList<string> Container(BackupTarget target, ContainerEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(endpoint);
        var arguments = new List<string> { "exec", "-i" };
        if (!string.IsNullOrEmpty(target.Password))
        {
            arguments.Add("--env");
            arguments.Add(BackupArguments.PasswordVariable);
        }

        arguments.Add(endpoint.Container);
        arguments.Add("pg_restore");
        arguments.Add("--no-owner");
        arguments.Add("--host=" + BackupArguments.ContainerLoopback);
        arguments.Add("--port=" + endpoint.ContainerPort.ToString(CultureInfo.InvariantCulture));
        arguments.Add("--username=" + target.User);
        arguments.Add("--no-password");
        arguments.Add("--dbname=" + target.Database);
        return arguments;
    }
}

public static class DatabaseRestore
{
    public static void Run(HostSettings settings, string backupFile)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!File.Exists(backupFile))
        {
            throw new HostingException($"The backup {backupFile} is missing.");
        }

        var tool = DatabaseBackup.Locate(settings);
        var connection = new NpgsqlConnectionStringBuilder(settings.Database.Connection);
        var target = new BackupTarget(connection.Host ?? "", connection.Port, connection.Username ?? "", connection.Database ?? "", connection.Password);
        DropSchema(settings);
        var environment = BackupArguments.Environment(target);
        using var source = File.OpenRead(backupFile);
        var result = tool.Container is { } endpoint
            ? ToolRunner.CaptureFrom(tool.Executable, RestoreArguments.Container(target, endpoint), environment, source)
            : ToolRunner.CaptureFrom(RestoreProgram(tool.Executable), [.. RestoreArguments.Local(target), backupFile], environment, Stream.Null);
        if (result.ExitCode != 0)
        {
            throw new HostingException($"pg_restore failed (exit {result.ExitCode}).", result.Output, null);
        }
    }

    private static string RestoreProgram(string pgDump)
    {
        var beside = Path.Combine(Path.GetDirectoryName(pgDump) ?? "", InstallPaths.Executable("pg_restore"));
        return File.Exists(beside) ? beside : ToolRunner.FindOnPath("pg_restore")
            ?? throw new HostingException("pg_restore was not found next to pg_dump or on PATH.", "Restore the backup by hand with pg_restore.", null);
    }

    private static void DropSchema(HostSettings settings)
    {
        try
        {
            using var dataSource = NpgsqlDataSource.Create(settings.Database.Connection);
            using var connection = dataSource.OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP SCHEMA IF EXISTS " + new NpgsqlCommandBuilder().QuoteIdentifier(settings.Database.Schema) + " CASCADE";
            command.ExecuteNonQuery();
        }
        catch (NpgsqlException ex)
        {
            throw DatabaseErrors.Explain(ex, new NpgsqlConnectionStringBuilder(settings.Database.Connection));
        }
    }
}
