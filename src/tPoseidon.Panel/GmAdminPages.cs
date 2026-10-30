using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public static class GmAccountsPage
{
    public static Control Build(GmKit kit, Action<string> useForLogin)
    {
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(useForLogin);
        var model = kit.Model;
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

        var registerSection = GmKit.Section(
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
                    async _ => await ShowAsync().ConfigureAwait(true))));

        var eagleSection = GmKit.Section(
            "GM in the game",
            "Turns the connected GM character into a GM in the game, or back into a normal character, without typing anything in the game's chat: the GM eagle appears over the character and the @ chat commands work. The eagle has two forms: walking with sword and shield, and flying with spread wings. The original has one eagle only; there is no female eagle. The character has to be in the game.",
            GmKit.Line(
                kit.Tool("Log my GM in as GM", "game-flag", _ => ["on"], primary: true),
                kit.Tool("Flying form", "fly", _ => ["on"]),
                kit.Tool("Walking form", "fly", _ => ["off"]),
                kit.Tool("Log my GM out", "game-flag", _ => ["off"])));

        var speed = GmKit.Choice(["1 (normal)", "2", "3", "4"], 110);
        var speedSection = GmKit.Section(
            "Speed, immortal and invisible",
            "These change the connected GM character in the game.",
            GmKit.Line(
                GmKit.Field("Speed", speed),
                kit.Tool("Set speed", "speed", _ => [Text((byte)(Math.Max(0, speed.SelectedIndex) + 1))]),
                kit.Tool("Immortal on", "immortal", _ => ["on"]),
                kit.Tool("Immortal off", "immortal", _ => ["off"]),
                kit.Tool("Invisible on", "invisible", _ => ["on"]),
                kit.Tool("Invisible off", "invisible", _ => ["off"])));

        var refresh = GmKit.Plain("Refresh");
        refresh.Click += async (_, _) => await ShowAsync().ConfigureAwait(true);
        var registeredSection = GmKit.Section(
            "Registered GM characters",
            null,
            registered,
            GmKit.Line(refresh, kit.Accounts("List in the results", GmAccounts.ListName, _ => [], _ => GmAccounts.List)));
        var page = GmKit.Tab(
            "Register and list GM characters, and switch your own GM character on as a GM in the game.",
            ("Registered GMs", [registeredSection, registerSection]),
            ("Your GM in the game", [eagleSection, speedSection]));
        page.AttachedToVisualTree += async (_, _) => await ShowAsync().ConfigureAwait(true);
        return page;
    }

    private static string Text(byte value) => value.ToString(CultureInfo.InvariantCulture);

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
        page.Children.Add(GmKit.Section(
            "GM chat commands in game",
            "The KR 2014 client has no GM chat of its own, so the server reads GM commands from normal chat lines that start with @. Type them as your GM character. Log in first with @login and your account password; every other command needs it. The line is never shown to other players. Words in <> are needed, words in [] are optional, and text with spaces goes in quotes.",
            chat));
        page.AttachedToVisualTree += async (_, _) =>
        {
            if (chat.Children.Count > 0)
            {
                return;
            }

            var catalog = await kit.Model.CatalogAsync().ConfigureAwait(true);
            foreach (var command in catalog.ChatCommands)
            {
                chat.Children.Add(Entry(command.Typed, command.Description, true));
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
        foreach (var line in GmAccounts.Help.Concat(GmPenalties.Help).Concat(GmMembership.Help))
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
