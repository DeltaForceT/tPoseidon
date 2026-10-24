using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public static class GmPenaltiesPage
{
    public static Control Build(GmKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var page = new StackPanel { Spacing = 10 };
        var code = GmKit.Box("Code", 80);
        var name = GmKit.Box("Name the player sees", 240);
        var kind = GmKit.Box("Kind (optional)", 110);
        page.Children.Add(GmKit.Section(
            "Penalty kinds",
            "A penalty kind has a code and the name the player sees at the lobby. Penalize accounts on the Players tab.",
            GmKit.Line(
                GmKit.Field("Code", code),
                GmKit.Field("Name", name),
                GmKit.Field("Kind", kind),
                kit.Penalty("Add or rename", GmPenalties.KindName, checks =>
                {
                    var number = checks.Number(code, "The code", short.MinValue, short.MaxValue);
                    var text = checks.Text(name, "The name");
                    return (kind.Text ?? "").Trim().Length == 0 ? [number, text] : [number, text, checks.Number(kind, "The kind", short.MinValue, short.MaxValue)];
                }, primary: true),
                kit.Penalty("Show kinds and running penalties", GmPenalties.ListName, _ => []))));

        var stepCode = GmKit.Box("Code", 80);
        var step = GmKit.Box("Step", 60, "1");
        var hours = GmKit.Box("Hours", 80, "72");
        page.Children.Add(GmKit.Section(
            "Penalty lengths",
            "How long the first, second, third... penalty of a kind lasts. A step without a length blocks for 9999 hours.",
            GmKit.Line(
                GmKit.Field("Code", stepCode),
                GmKit.Field("Step (1 = first time)", step),
                GmKit.Field("Hours", hours),
                kit.Penalty("Set the length", GmPenalties.TimeName, checks =>
                {
                    var number = checks.Number(stepCode, "The code", short.MinValue, short.MaxValue);
                    var count = checks.Number(step, "The step", 1, short.MaxValue);
                    return [number, count, checks.Number(hours, "The hours", 1, int.MaxValue)];
                }))));
        return page;
    }
}

public static class GmAccountsPage
{
    public static Control Build(GmKit kit, Action<string> useForLogin)
    {
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(useForLogin);
        var model = kit.Model;
        var page = new StackPanel { Spacing = 10 };
        var registered = new StackPanel { Spacing = 2 };
        var character = GmKit.Box("Character name", 200);
        var address = GmKit.Box("IP address", 140, GmAccounts.LocalPermitIp);
        model.CharacterChosen += chosen => character.Text = chosen;

        async Task ShowAsync()
        {
            var entries = await model.GameMastersAsync().ConfigureAwait(true);
            registered.Children.Clear();
            if (entries.Count == 0)
            {
                registered.Children.Add(Tokens.Caption("No GM character is registered yet."));
            }

            foreach (var entry in entries)
            {
                registered.Children.Add(Row(kit, entry, useForLogin, ShowAsync));
            }
        }

        page.AttachedToVisualTree += async (_, _) => await ShowAsync().ConfigureAwait(true);
        page.Children.Add(GmKit.Section(
            "Register a GM character",
            "Makes the character its account's GM character (one per account) and allows GM login from the address. The window connects from this computer, so keep 127.0.0.1 unless you log in to the game as GM from another address. The servers may keep running.",
            GmKit.Line(
                GmKit.Field("Character", character),
                GmKit.Field("GM login allowed from", address),
                kit.Accounts("Register", GmAccounts.RegisterName, checks =>
                {
                    var name = checks.Text(character, "The character name");
                    var ip = checks.Text(address, "The address");
                    checks.Require(ip.Length == 0 || GmAccounts.IsPermitAddress(ip), address, "The address must be an IP address, like 127.0.0.1.");
                    return [name, ip];
                }, arguments => book => GmAccounts.Register(book, arguments[0], arguments[1]), done: async _ => await ShowAsync().ConfigureAwait(true), primary: true),
                kit.Accounts("Unregister", GmAccounts.UnregisterName, checks => [checks.Text(character, "The character name")],
                    arguments => book => GmAccounts.Unregister(book, arguments[0]),
                    arguments => new GmConfirm($"Take GM away from {arguments[0]}?", "Its GM login addresses are removed and the account becomes a normal account.", "Unregister"),
                    async _ => await ShowAsync().ConfigureAwait(true)))));

        var refresh = GmKit.Plain("Refresh");
        refresh.Click += async (_, _) => await ShowAsync().ConfigureAwait(true);
        page.Children.Add(GmKit.Section(
            "Registered GM characters",
            null,
            registered,
            GmKit.Line(refresh, kit.Accounts("List in the results", GmAccounts.ListName, _ => [], _ => GmAccounts.List))));
        return page;
    }

    private static Border Row(GmKit kit, GmCharacterEntry entry, Action<string> useForLogin, Func<Task> reload)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("180,*,Auto,Auto") };
        var name = new TextBlock { Text = entry.CharacterName, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var detail = Tokens.Caption($"account {entry.AccountName} · GM login from {(entry.PermittedIps.Count == 0 ? "no address" : string.Join(", ", entry.PermittedIps))} · since {entry.CreatedAt.ToLocalTime():yyyy-MM-dd}");
        detail.TextTrimming = TextTrimming.CharacterEllipsis;
        var use = GmKit.Plain("Use to connect");
        use.Margin = new Thickness(6, 0, 0, 0);
        use.Click += (_, _) => useForLogin(entry.CharacterName);
        var remove = kit.Accounts("Unregister", GmAccounts.UnregisterName, _ => [entry.CharacterName],
            arguments => book => GmAccounts.Unregister(book, arguments[0]),
            arguments => new GmConfirm($"Take GM away from {arguments[0]}?", "Its GM login addresses are removed and the account becomes a normal account.", "Unregister"),
            async _ => await reload().ConfigureAwait(true));
        remove.Margin = new Thickness(6, 0, 0, 0);
        Grid.SetColumn(detail, 1);
        Grid.SetColumn(use, 2);
        Grid.SetColumn(remove, 3);
        grid.Children.Add(name);
        grid.Children.Add(detail);
        grid.Children.Add(use);
        grid.Children.Add(remove);
        return new Border { Background = Tokens.Surface0, CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4), Child = grid };
    }
}

public static class GmHelpPage
{
    public static Control Build(GmKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var page = new StackPanel { Spacing = 10 };
        var chat = new StackPanel { Spacing = 2 };
        var source = GmKit.Hint("Reading the client's GM keywords...");
        page.Children.Add(GmKit.Section(
            "GM chat commands in game",
            "Type these in the game's chat as your GM character. Log in first with the login command; every other command needs it. Words in <> are needed, words in [] are optional, and text with spaces goes in quotes.",
            source,
            chat));
        page.AttachedToVisualTree += async (_, _) =>
        {
            if (chat.Children.Count > 0)
            {
                return;
            }

            var catalog = await kit.Model.CatalogAsync().ConfigureAwait(true);
            source.Text = catalog.Source is { } file
                ? $"The keywords come from the client's game data ({file}), the same file the servers read."
                : catalog.Problem ?? "";
            foreach (var command in catalog.ChatCommands)
            {
                chat.Children.Add(Entry(command.Keyword is null ? $"{command.Name} (no keyword in the client data, so it is not a chat command)" : command.Typed, command.Description, command.Keyword is not null));
            }
        };

        var tools = new StackPanel { Spacing = 2 };
        foreach (var command in GameMasterToolCommands.All)
        {
            tools.Children.Add(Entry($"{command.Name} {command.Arguments}".TrimEnd(), command.Description, true));
        }

        page.Children.Add(GmKit.Section(
            "Tool commands",
            "Everything in this window runs one of these, the same as tposeidon gm <command> on the command line. The result area shows the command each button ran.",
            tools));

        var admin = new StackPanel { Spacing = 2 };
        foreach (var line in GmAccounts.Help.Concat(GmPenalties.Help))
        {
            admin.Children.Add(new TextBlock { Text = line, FontFamily = Tokens.MonoFont, FontSize = 11.5, Foreground = Tokens.TextSecondary });
        }

        page.Children.Add(GmKit.Section("Database commands", "These change the database directly, so they work while the game servers are stopped (PostgreSQL must run).", admin));
        return page;
    }

    private static Grid Entry(string usage, string description, bool available)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*"), Margin = new Thickness(0, 1) };
        var left = new SelectableTextBlock { Text = usage, FontFamily = Tokens.MonoFont, FontSize = 11.5, Foreground = available ? Tokens.Accent : Tokens.TextMuted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 0) };
        var right = new TextBlock { Text = description, Foreground = Tokens.TextSecondary, FontSize = Tokens.FontSizeCaption, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }
}
