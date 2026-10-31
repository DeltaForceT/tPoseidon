using tPoseidon.Cli.Terminal;
using tPoseidon.Hosting;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Cli.Commands;

internal static class AccountCommand
{
    private const string Add = "add";
    private const string Password = "password";
    private const string List = "list";
    private const string PasswordOption = "password";

    public static int Run(InstallPaths paths, CommandLine commandLine, Prompt prompt)
    {
        commandLine.RejectUnknown(PasswordOption);
        var action = commandLine.Positionals.Count > 0 ? commandLine.Positionals[0] : throw new CliException(Usage);
        var expected = action == List ? 1 : 2;
        if (action is not (Add or Password or List))
        {
            throw new CliException($"Unknown account action '{action}'. {Usage}");
        }

        if (commandLine.Positionals.Count > expected)
        {
            throw new CliException($"Unexpected argument '{commandLine.Positionals[expected]}'.");
        }

        var settings = CommandSupport.LoadSettings(paths);
        var book = new DatabasePlayerAccountBook(settings.Database);
        if (action == List)
        {
            return Show(GmPlayerAccounts.List(book));
        }

        if (commandLine.Positionals.Count < 2)
        {
            throw new CliException($"Say which account: tposeidon account {action} <name>");
        }

        var name = commandLine.Positionals[1].Trim();
        if (GmPlayerAccounts.NameProblem(name) is { } nameProblem)
        {
            throw new CliException(nameProblem);
        }

        if (action == Add && book.Exists(name))
        {
            throw new CliException($"There is already an account named '{name}'. Use: tposeidon account password {name}");
        }

        var password = commandLine.Option(PasswordOption) ?? AskPassword(prompt, name);
        return Show(action == Add ? GmPlayerAccounts.Add(book, name, password) : GmPlayerAccounts.SetPassword(book, name, password));
    }

    public static void WriteHelp()
    {
        Console.WriteLine("account options:");
        Console.WriteLine("  account add <name> [--password <text>]        create a player account; asks for the password twice without --password");
        Console.WriteLine("  account password <name> [--password <text>]   set a new password for an account");
        Console.WriteLine("  account list                                  show the accounts, when they were made, GM and membership marks");
    }

    private static string Usage => "Use: tposeidon account add <name> | account password <name> | account list";

    private static string AskPassword(Prompt prompt, string name)
    {
        while (true)
        {
            var password = prompt.AskSecret($"Password for '{name}'");
            if (password.Length > 0 && prompt.AskSecret("Same password again") == password)
            {
                return password;
            }

            Console.WriteLine(password.Length == 0 ? "The password cannot be empty." : "The passwords did not match. Try again.");
        }
    }

    private static int Show(GmOutcome outcome)
    {
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
}
