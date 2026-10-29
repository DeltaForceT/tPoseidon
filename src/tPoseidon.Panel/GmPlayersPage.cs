using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public static class GmPlayersPage
{
    public static Control Build(GmKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var model = kit.Model;
        var name = GmKit.Box("Character name", 200);
        var account = GmKit.Box("Account name", 170);
        var characterId = GmKit.Box("Character id", 130);
        model.CharacterChosen += chosen => name.Text = chosen;

        string Who(GmChecks checks) => checks.Text(name, "The character name");
        string MemoText(GmChecks checks, TextBox box, string label, bool required) => checks.Text(box, label, GameMasterFrames.MemoLength, required);

        var findSection = FindSection(kit, name, account, characterId);

        var counted = GmPickers.Map(model);
        var whoSection = GmKit.Section(
            "Who is where",
            null,
            GmKit.Line(kit.Tool("Characters in every zone", "zone-counts", _ => [], primary: true)),
            counted,
            GmKit.Line(kit.Tool("Characters on this map", "char-count", checks => [checks.Number(counted.Id, "The map id", 0, ushort.MaxValue)])));

        var item = GmPickers.Item(model);
        var count = GmKit.Box("Count", 80, "1");
        var giveSection = GmKit.Section(
            "Give item",
            "Puts the items in the bag of an online character.",
            item,
            GmKit.Line(
                GmKit.Field("Count", count),
                kit.Tool("Give", "present", checks =>
                {
                    var who = Who(checks);
                    var id = checks.Number(item.Id, "The item id", 0, uint.MaxValue);
                    return [who, id, checks.Number(count, "Count", 1, ushort.MaxValue)];
                }, primary: true)));

        var memo = GmKit.Lines("Memo text");
        var memoSection = GmKit.Section(
            "Memo",
            null,
            memo,
            GmKit.Line(kit.Tool("Send memo", "memo", checks =>
            {
                var who = Who(checks);
                return [who, MemoText(checks, memo, "The memo", true)];
            })));

        var reason = GmKit.Box("Reason the player reads (optional)", 320);
        var kickSection = GmKit.Section(
            "Kick",
            "Without a reason the player is disconnected at once. With a reason he reads it first, is disconnected when his client confirms it (about 10 seconds), and his account is marked sanctioned.",
            GmKit.Line(
                GmKit.Field("Reason", reason),
                kit.Tool("Kick", "kick", checks =>
                {
                    var who = Who(checks);
                    var why = MemoText(checks, reason, "The reason", false);
                    return why.Length == 0 ? [who] : [who, why];
                }, arguments => new GmConfirm($"Kick {arguments[0]}?", arguments.Count > 1 ? $"They read \"{arguments[1]}\" and are disconnected; their account is marked sanctioned." : "They are disconnected at once.", "Kick"))));

        var closeSection = GmKit.Section(
            "Close what they opened",
            "My Camp by name works for an online character; by id for any character.",
            GmKit.Line(
                kit.Tool("Close My Camp", "remove-camp", checks => [Who(checks)], arguments => new GmConfirm($"Close the My Camp of {arguments[0]}?", "Visitors are sent out.", "Close")),
                kit.Tool("Close My Camp by id", "remove-camp-id", checks =>
                {
                    var id = checks.Text(characterId, "The character id");
                    checks.Require(id.Length == 0 || GameMasterToolCommands.TryParseCharacterId(id, out _), characterId, "The character id must be a number (decimal or 0x hex).");
                    return [id];
                }, arguments => new GmConfirm($"Close the My Camp of character {arguments[0]}?", "Visitors are sent out.", "Close")),
                kit.Tool("Close personal shop", "remove-shop", checks => [Who(checks)], arguments => new GmConfirm($"Close the personal shop of {arguments[0]}?", "The shop closes at once.", "Close")),
                kit.Tool("Close chat room", "remove-chat", checks => [Who(checks)], arguments => new GmConfirm($"Close the instant chat room of {arguments[0]}?", "Everyone in the room leaves it.", "Close"))));

        return GmKit.Tab(
            "Find a player, then look at them, move them, give them items, send a memo or remove them from the game.",
            ("Find", [findSection, whoSection]),
            ("Help or move", [MoveSection(kit, Who), giveSection, memoSection]),
            ("Remove", [kickSection, closeSection]));
    }

    private static Border FindSection(GmKit kit, TextBox name, TextBox account, TextBox characterId)
    {
        var model = kit.Model;
        var search = GmKit.Box("Part of a character or account name", 260);
        var results = new ListBox { Height = 120, Background = Tokens.Surface0, BorderBrush = Tokens.BorderSubtle, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
        var note = GmKit.Hint("Searches every character in the database. Look up asks the server whether one is online.");
        var find = GmKit.Plain("Find");
        async Task FindAsync()
        {
            var found = await model.FindCharactersAsync(search.Text ?? "").ConfigureAwait(true);
            var catalog = await model.CatalogAsync().ConfigureAwait(true);
            results.ItemsSource = found.Select(character => new ListBoxItem
            {
                Tag = character,
                Content = new TextBlock
                {
                    Text = $"{character.Name} · Lv {character.Level} · account {character.AccountName} · {catalog.MapName(character.MapId) ?? "map"} ({character.MapId})",
                    Margin = new Thickness(8, 3),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
            }).ToList();
            note.Text = found.Count == 0 ? "No character matches." : $"{found.Count} character(s). Pick one to fill the fields below.";
        }

        find.Click += async (_, _) => await FindAsync().ConfigureAwait(true);
        search.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                await FindAsync().ConfigureAwait(true);
            }
        };
        results.SelectionChanged += (_, _) =>
        {
            if (results.SelectedItem is ListBoxItem { Tag: CharacterSummary character })
            {
                name.Text = character.Name;
                account.Text = character.AccountName;
                characterId.Text = "0x" + character.CharacterId.ToString("X", CultureInfo.InvariantCulture);
                model.Choose(character.Name, character.AccountName);
            }
        };

        return GmKit.Section(
            "Character",
            null,
            GmKit.Line(GmKit.Field("Search", search), find),
            results,
            note,
            GmKit.Line(
                GmKit.Field("Character", name),
                GmKit.Field("Account", account),
                GmKit.Field("Character id", characterId),
                kit.Tool("Look up", "char-info", checks => [checks.Text(name, "The character name")], primary: true)));
    }


    private static Border MoveSection(GmKit kit, Func<GmChecks, string> who)
    {
        var model = kit.Model;
        var map = GmPickers.Map(model);
        var other = GmKit.Box("Other character", 200);
        string Gm(GmChecks checks)
        {
            checks.Require(model.GameMaster is not null, null, "Connect first: your GM character is not known yet.");
            return model.GameMaster ?? "";
        }

        return GmKit.Section(
            "Move",
            "Teleports change the zone, so the player sees a loading screen.",
            map,
            GmKit.Line(
                kit.Tool("Send to this map", "teleport-zone", checks =>
                {
                    var character = who(checks);
                    return [character, checks.Number(map.Id, "The map id", 1, 999)];
                }, primary: true)),
            GmKit.Line(
                GmKit.Field("Next to", other),
                kit.Tool("Send next to them", "teleport", checks =>
                {
                    var character = who(checks);
                    return [character, checks.Text(other, "The other character")];
                }),
                kit.Tool("Bring to my GM", "teleport", checks =>
                {
                    var character = who(checks);
                    return [character, Gm(checks)];
                }),
                kit.Tool("Take my GM to them", "teleport", checks => [who(checks)])));
    }

}
