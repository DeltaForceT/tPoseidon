using tPoseidon.Core.Common;

namespace tPoseidon.Tools.ReleasePack;

internal static class SigningCommands
{
    public static int Run(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 1; i < args.Count; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Count)
            {
                return Usage();
            }

            options[args[i][2..]] = args[i + 1];
        }

        try
        {
            if (args[0] == "make-key")
            {
                if (!options.TryGetValue("output", out var folder) || options.Count != 1)
                {
                    return Usage();
                }

                var publicKey = ReleaseSigning.MakeKey(folder, RepoRoot.Path);
                Console.WriteLine($"The private key is in {Path.GetFullPath(folder)}. Keep it there and back it up; never commit it.");
                Console.WriteLine("Public key (put it in src/tPoseidon.Hosting/Updates/ReleaseKey.cs):");
                Console.WriteLine(publicKey);
                return 0;
            }

            if (!options.TryGetValue("key", out var key) || !options.TryGetValue("input", out var input) || options.Keys.Any(name => name is not ("key" or "input" or "output")))
            {
                return Usage();
            }

            var files = ReleaseSigning.SignableFilesIn(input);
            if (files.Count == 0)
            {
                throw new InvalidDataException($"{input} has no tposeidon-*.zip or tposeidon-*.tar.gz packages.");
            }

            foreach (var written in ReleaseSigning.Sign(files, options.GetValueOrDefault("output") ?? input, key))
            {
                Console.WriteLine($"signed: {written}");
            }

            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            Console.Error.WriteLine("error: " + ex.Message);
            return 1;
        }
    }

    private static int Usage()
    {
        PackOptions.PrintUsage();
        return 2;
    }
}
