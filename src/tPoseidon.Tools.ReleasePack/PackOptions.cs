namespace tPoseidon.Tools.ReleasePack;

internal sealed record PackOptions(string Version, IReadOnlyList<string> Rids, string? Output, string? Cache)
{
    public static readonly IReadOnlyList<string> AllRids = new[] { "win-x64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" };

    public static PackOptions? Parse(IReadOnlyList<string> args)
    {
        string? version = null;
        string? output = null;
        string? cache = null;
        var rids = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            if (i + 1 >= args.Count)
            {
                return null;
            }

            var value = args[++i];
            switch (args[i - 1])
            {
                case "--version":
                    version = value;
                    break;
                case "--rid":
                    rids.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--output":
                    output = value;
                    break;
                case "--cache":
                    cache = value;
                    break;
                default:
                    return null;
            }
        }

        if (version is null)
        {
            return null;
        }

        if (rids.Count == 0)
        {
            rids = AllRids.ToList();
        }
        else if (rids.Contains("all", StringComparer.Ordinal))
        {
            rids = AllRids.ToList();
        }

        var unknown = rids.Where(rid => !AllRids.Contains(rid, StringComparer.Ordinal)).ToList();
        if (unknown.Count > 0)
        {
            Console.Error.WriteLine($"Unknown runtime identifier(s): {string.Join(", ", unknown)}.");
            return null;
        }

        return new PackOptions(version, rids, output, cache);
    }

    public static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: dotnet run --project src/tPoseidon.Tools.ReleasePack -- --version <0.55.0-beta> [--rid all|win-x64,linux-x64,...] [--output <folder>] [--cache <folder>]");
        Console.Error.WriteLine($"Runtime identifiers: {string.Join(", ", AllRids)}");
    }
}
