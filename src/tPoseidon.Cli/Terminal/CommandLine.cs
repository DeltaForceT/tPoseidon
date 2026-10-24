namespace tPoseidon.Cli.Terminal;

internal sealed class CommandLine
{
    private static readonly HashSet<string> Switches = new(StringComparer.Ordinal) { "yes", "help" };

    private readonly Dictionary<string, string> _options;
    private readonly HashSet<string> _switches;

    private CommandLine(string command, IReadOnlyList<string> positionals, Dictionary<string, string> options, HashSet<string> switches)
    {
        Command = command;
        Positionals = positionals;
        _options = options;
        _switches = switches;
    }

    public IReadOnlyList<string> Positionals { get; }

    public string Command { get; }

    public bool AssumeDefaults => _switches.Contains("yes");

    public bool Help => _switches.Contains("help");

    public string? Option(string name) => _options.TryGetValue(name, out var value) ? value : null;

    public void RejectUnknown(params string[] allowed)
    {
        var unknown = _options.Keys.Where(key => !allowed.Contains(key, StringComparer.Ordinal) && key != "home").ToList();
        if (unknown.Count > 0)
        {
            throw new CliException($"'{Command}' does not take --{string.Join(", --", unknown)}.");
        }
    }

    public static CommandLine Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        var switches = new HashSet<string>(StringComparer.Ordinal);
        string? command = null;
        var positionals = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg is "-h" or "-?")
            {
                switches.Add("help");
                continue;
            }

            if (arg is "-y")
            {
                switches.Add("yes");
                continue;
            }

            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                var name = arg[2..];
                var equals = name.IndexOf('=', StringComparison.Ordinal);
                if (equals > 0)
                {
                    options[name[..equals]] = name[(equals + 1)..];
                    continue;
                }

                if (Switches.Contains(name))
                {
                    switches.Add(name);
                    continue;
                }

                if (i + 1 >= args.Count)
                {
                    throw new CliException($"--{name} needs a value.");
                }

                options[name] = args[++i];
                continue;
            }

            if (command is not null)
            {
                positionals.Add(arg);
                continue;
            }

            command = arg;
        }

        return new CommandLine(command ?? "help", positionals, options, switches);
    }
}
