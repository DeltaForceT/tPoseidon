using System.Text;

namespace tPoseidon.Cli.Terminal;

internal sealed class Prompt
{
    private readonly bool _assumeDefaults;

    public Prompt(bool assumeDefaults)
    {
        _assumeDefaults = assumeDefaults || Console.IsInputRedirected;
    }

    public bool Interactive => !_assumeDefaults;

    public string Ask(string question, string defaultValue)
    {
        if (_assumeDefaults)
        {
            Console.WriteLine($"{question}: {defaultValue}");
            return defaultValue;
        }

        Console.Write(string.IsNullOrEmpty(defaultValue) ? $"{question}: " : $"{question} [{defaultValue}]: ");
        var answer = Console.ReadLine();
        return string.IsNullOrWhiteSpace(answer) ? defaultValue : answer.Trim();
    }

    public bool Confirm(string question, bool defaultValue)
    {
        var hint = defaultValue ? "Y/n" : "y/N";
        if (_assumeDefaults)
        {
            Console.WriteLine($"{question} [{hint}]: {(defaultValue ? "y" : "n")}");
            return defaultValue;
        }

        while (true)
        {
            Console.Write($"{question} [{hint}]: ");
            var answer = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(answer))
            {
                return defaultValue;
            }

            if (answer.StartsWith('y') || answer.StartsWith('Y'))
            {
                return true;
            }

            if (answer.StartsWith('n') || answer.StartsWith('N'))
            {
                return false;
            }
        }
    }

    public T Choose<T>(string question, IReadOnlyList<(T Value, string Label)> options, T defaultValue)
        where T : notnull
    {
        Console.WriteLine(question);
        var defaultIndex = 0;
        for (var i = 0; i < options.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(options[i].Value, defaultValue))
            {
                defaultIndex = i;
            }

            Console.WriteLine($"  {i + 1}. {options[i].Label}");
        }

        while (true)
        {
            var answer = Ask("Choice", (defaultIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (int.TryParse(answer, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var picked)
                && picked >= 1 && picked <= options.Count)
            {
                return options[picked - 1].Value;
            }

            Console.WriteLine($"Pick a number from 1 to {options.Count}.");
        }
    }

    public string AskSecret(string question)
    {
        if (_assumeDefaults)
        {
            throw new CliException($"{question} is needed; pass it on the command line when running without a terminal.");
        }

        Console.Write($"{question}: ");
        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return builder.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (builder.Length > 0)
                {
                    builder.Length--;
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                builder.Append(key.KeyChar);
            }
        }
    }
}
