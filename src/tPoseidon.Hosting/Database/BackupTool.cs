using System.Globalization;
using System.Text;

namespace tPoseidon.Hosting.Database;

public sealed record ContainerEndpoint(string Container, int ContainerPort);

public sealed record PublishedPort(string HostAddress, int HostFirst, int HostLast, int ContainerFirst);

public sealed record BackupTarget(string Host, int Port, string User, string Database, string? Password);

public enum DockerStatus
{
    NotInstalled,
    Unavailable,
    NoMatchingContainer,
    Found,
}

public sealed record DockerLookup(DockerStatus Status, string? Executable, ContainerEndpoint? Endpoint, string Detail)
{
    public static DockerLookup NotInstalled { get; } = new(DockerStatus.NotInstalled, null, null, "");

    public static DockerLookup Unavailable(string executable, string detail) => new(DockerStatus.Unavailable, executable, null, detail);

    public static DockerLookup NoMatch(string executable) => new(DockerStatus.NoMatchingContainer, executable, null, "");

    public static DockerLookup Match(string executable, ContainerEndpoint endpoint) => new(DockerStatus.Found, executable, endpoint, "");
}

public sealed record BackupTool(string Executable, ContainerEndpoint? Container)
{
    public bool InContainer => Container is not null;

    public string Describe() =>
        Container is { } endpoint
            ? $"pg_dump inside the Docker container {endpoint.Container}"
            : Executable;
}

public sealed record BackupChoice(BackupTool? Tool, DockerLookup? Docker);

public static class DockerPorts
{
    public const string PsFormat = "{{.Names}}\t{{.Ports}}";

    private const string PortSeparator = "->";
    private const char RangeSeparator = '-';
    private static readonly string[] LoopbackHosts = ["localhost", "127.0.0.1", "::1", "[::1]"];
    private static readonly string[] WildcardAddresses = ["0.0.0.0", "[::]", "::", "*"];

    public static bool IsLoopback(string host) => LoopbackHosts.Contains(host, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<PublishedPort> ParsePorts(string ports)
    {
        ArgumentNullException.ThrowIfNull(ports);
        var parsed = new List<PublishedPort>();
        foreach (var entry in ports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var arrow = entry.IndexOf(PortSeparator, StringComparison.Ordinal);
            if (arrow < 0)
            {
                continue;
            }

            var left = entry[..arrow];
            var right = entry[(arrow + PortSeparator.Length)..];
            var colon = left.LastIndexOf(':');
            var slash = right.IndexOf('/', StringComparison.Ordinal);
            if (colon < 0 || !TryRange(left[(colon + 1)..], out var first, out var last) || !TryRange(slash < 0 ? right : right[..slash], out var containerFirst, out _))
            {
                continue;
            }

            parsed.Add(new PublishedPort(left[..colon], first, last, containerFirst));
        }

        return parsed;
    }

    public static ContainerEndpoint? Find(string psOutput, string host, int port, IReadOnlyCollection<string> localAddresses)
    {
        ArgumentNullException.ThrowIfNull(psOutput);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(localAddresses);
        foreach (var line in psOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tab = line.IndexOf('\t', StringComparison.Ordinal);
            if (tab <= 0)
            {
                continue;
            }

            var name = line[..tab].Split(',')[0].Trim();
            foreach (var published in ParsePorts(line[(tab + 1)..]))
            {
                if (port >= published.HostFirst && port <= published.HostLast && ServesHost(published.HostAddress, host, localAddresses))
                {
                    return new ContainerEndpoint(name, published.ContainerFirst + (port - published.HostFirst));
                }
            }
        }

        return null;
    }

    private static bool ServesHost(string published, string host, IReadOnlyCollection<string> localAddresses)
    {
        var wildcard = WildcardAddresses.Contains(published, StringComparer.Ordinal);
        if (IsLoopback(host))
        {
            return wildcard || IsLoopback(published);
        }

        return string.Equals(published, host, StringComparison.OrdinalIgnoreCase)
            || (wildcard && localAddresses.Contains(host, StringComparer.OrdinalIgnoreCase));
    }

    private static bool TryRange(string text, out int first, out int last)
    {
        var dash = text.IndexOf(RangeSeparator, StringComparison.Ordinal);
        if (dash < 0)
        {
            var single = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out first);
            last = first;
            return single;
        }

        last = 0;
        return int.TryParse(text[..dash], NumberStyles.None, CultureInfo.InvariantCulture, out first)
            && int.TryParse(text[(dash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out last)
            && last >= first;
    }
}

public static class BackupToolChooser
{
    public static BackupChoice Choose(string? localPgDump, Func<DockerLookup> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        if (!string.IsNullOrEmpty(localPgDump))
        {
            return new BackupChoice(new BackupTool(localPgDump, null), null);
        }

        var docker = lookup();
        return docker is { Status: DockerStatus.Found, Executable: { } executable, Endpoint: { } endpoint }
            ? new BackupChoice(new BackupTool(executable, endpoint), docker)
            : new BackupChoice(null, docker);
    }

    public static string NoToolHint(string host, int port, DockerLookup? docker)
    {
        var text = new StringBuilder();
        text.Append("There are two ways to fix this. ");
        text.Append("1) Install the PostgreSQL client tools and add PostgreSQL's bin folder to PATH, so that pg_dump is found (see docs/install/postgresql.md). ");
        text.Append($"2) If PostgreSQL runs in Docker, have Docker running with the container started and publishing port {port.ToString(CultureInfo.InvariantCulture)} on {host}; tPoseidon then runs pg_dump inside that container. ");
        text.Append(DockerNote(port, docker));
        text.Append(" To start the servers without a backup, run 'tposeidon start --no-backup' or use Start without a backup in the panel.");
        return text.ToString();
    }

    private static string DockerNote(int port, DockerLookup? docker) =>
        docker?.Status switch
        {
            DockerStatus.NotInstalled => "What was found: pg_dump is not on PATH and the docker command is not installed.",
            DockerStatus.Unavailable => $"What was found: pg_dump is not on PATH and docker did not answer ({docker.Detail}).",
            DockerStatus.NoMatchingContainer => $"What was found: pg_dump is not on PATH and no running Docker container publishes port {port.ToString(CultureInfo.InvariantCulture)}.",
            _ => "What was found: pg_dump is not on PATH.",
        };
}

public static class BackupArguments
{
    public const string PasswordVariable = "PGPASSWORD";
    public const string ContainerLoopback = "127.0.0.1";

    public static IReadOnlyList<string> Local(BackupTarget target, string file)
    {
        ArgumentNullException.ThrowIfNull(target);
        return
        [
            "--format=custom",
            "--file=" + file,
            "--host=" + target.Host,
            "--port=" + target.Port.ToString(CultureInfo.InvariantCulture),
            "--username=" + target.User,
            "--no-password",
            target.Database,
        ];
    }

    public static IReadOnlyList<string> Container(BackupTarget target, ContainerEndpoint endpoint)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(endpoint);
        var arguments = new List<string> { "exec" };
        if (!string.IsNullOrEmpty(target.Password))
        {
            arguments.Add("--env");
            arguments.Add(PasswordVariable);
        }

        arguments.Add(endpoint.Container);
        arguments.Add("pg_dump");
        arguments.Add("--format=custom");
        arguments.Add("--host=" + ContainerLoopback);
        arguments.Add("--port=" + endpoint.ContainerPort.ToString(CultureInfo.InvariantCulture));
        arguments.Add("--username=" + target.User);
        arguments.Add("--no-password");
        arguments.Add(target.Database);
        return arguments;
    }

    public static IReadOnlyDictionary<string, string> Environment(BackupTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(target.Password))
        {
            variables[PasswordVariable] = target.Password;
        }

        return variables;
    }
}
