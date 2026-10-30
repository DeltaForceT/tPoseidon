using System.Globalization;
using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Cli.Commands;

internal static class CampCommand
{
    public static int Run(InstallPaths paths, CommandLine commandLine)
    {
        commandLine.RejectUnknown("character", "give", "count");
        var settings = CommandSupport.LoadSettings(paths);
        var name = commandLine.Option("character")?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new CliException("Say which character: tposeidon camp --character <name> [--give <camp item id> [--count <n>]]");
        }

        var gifts = new DatabaseGmCampGifts(settings.Database);
        var give = commandLine.Option("give");
        var outcome = give is null ? GmCamp.Stock(gifts, name) : GmCamp.Give(gifts, name, Number(give, "a My Camp item id"), CountOf(commandLine));
        if (outcome.Refused)
        {
            throw new CliException(outcome.Lines[0]);
        }

        foreach (var line in outcome.Lines)
        {
            Console.WriteLine(line);
        }

        return 0;
    }

    private static int CountOf(CommandLine commandLine) =>
        commandLine.Option("count") is { } text ? Number(text, "a count") : 1;

    private static int Number(string text, string what) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new CliException($"'{text}' is not {what}.");
}
