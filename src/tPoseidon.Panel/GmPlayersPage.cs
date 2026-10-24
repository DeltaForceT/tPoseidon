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
    public const uint MaximumBlockDays = uint.MaxValue / GameMasterChatBlocks.MinutesPerDay;

    public static IReadOnlyList<(string Label, byte Type)> TimedBlocks { get; } =
    [
        ("Chat", GameMasterBlocks.Chat),
        ("Move", GameMasterBlocks.Move),
        ("Trade", GameMasterBlocks.Trade),
        ("My Camp", GameMasterBlocks.MyCamp),
        ("Personal shop", GameMasterBlocks.PersonalShop),
        ("Chat room", GameMasterBlocks.ChatRoom),
    ];

    public static (string Label, string Value)[] MinutePresets { get; } =
    [
        ("10 min", "10"),
        ("30 min", "30"),
        ("1 hour", "60"),
        ("1 day", Minutes(1)),
        ("7 days", Minutes(7)),
        ("15 days", GameMasterChatBlocks.PermanentMinutes.ToString(CultureInfo.InvariantCulture)),
    ];

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

        var page = new StackPanel { Spacing = 10 };
        page.Children.Add(FindSection(kit, name, account, characterId));

        var reason = GmKit.Box("Reason the player reads (optional)", 320);
        page.Children.Add(GmKit.Section(
            "Kick",
            "Without a reason the player is disconnected at once. With a reason he reads it first, is disconnected when his client confirms it (about 10 seconds), and his account is marked sanctioned.",
            GmKit.Line(
                GmKit.Field("Reason", reason),
                kit.Tool("Kick", "kick", checks =>
                {
                    var who = Who(checks);
                    var why = MemoText(checks, reason, "The reason", false);
                    return why.Length == 0 ? [who] : [who, why];
                }, arguments => new GmConfirm($"Kick {arguments[0]}?", arguments.Count > 1 ? $"They read \"{arguments[1]}\" and are disconnected; their account is marked sanctioned." : "They are disconnected at once.", "Kick")))));

        var blockType = GmKit.Choice(TimedBlocks.Select(block => block.Label), 150);
        var blockMinutes = GmKit.Box("Minutes", 90, "30");
        byte TypeOf() => TimedBlocks[Math.Max(0, blockType.SelectedIndex)].Type;
        page.Children.Add(GmKit.Section(
            "Timed blocks",
            "These work for offline characters too, and are saved in the database.",
            GmKit.Line(
                GmKit.Field("Block", blockType),
                GmKit.Field("Minutes", blockMinutes),
                GmKit.Presets(blockMinutes, MinutePresets)),
            GmKit.Line(
                kit.Tool("Block", "block", checks =>
                {
                    var minutes = checks.Number(blockMinutes, "Minutes", 1, uint.MaxValue);
                    return [Text(TypeOf()), minutes, Who(checks)];
                }, arguments => new GmConfirm($"{TimedBlocks.First(block => Text(block.Type) == arguments[0]).Label} block {arguments[2]}?", $"For {arguments[1]} minute(s).", "Block")),
                kit.Tool("Lift this block", "unblock", checks => [Text(TypeOf()), Who(checks)],
                    arguments => new GmConfirm($"Lift the {TimedBlocks.First(block => Text(block.Type) == arguments[0]).Label.ToLowerInvariant()} block of {arguments[1]}?", "They can use it again at once.", "Lift block")))));

        var days = GmKit.Box("Days", 70, "1");
        var cause = GmKit.Box("Reason the player reads", 320);
        page.Children.Add(GmKit.Section(
            "Normal block",
            "Blocks an online character from playing. With a reason the player reads it and is disconnected when he confirms it.",
            GmKit.Line(
                GmKit.Field("Days", days),
                GmKit.Presets(days, ("1 day", "1"), ("3 days", "3"), ("7 days", "7"), ("15 days", GameMasterChatBlocks.PermanentDays.ToString(CultureInfo.InvariantCulture))),
                GmKit.Field("Reason", cause)),
            GmKit.Line(
                kit.Tool("Block with the reason", "block-cause", checks =>
                {
                    var who = Who(checks);
                    var length = checks.Number(days, "Days", 1, MaximumBlockDays);
                    var why = MemoText(checks, cause, "The reason", true);
                    return [who, MinutesOf(length), why];
                }, arguments => new GmConfirm($"Block {arguments[0]} with a reason?", $"For {arguments[1]} minute(s). They read \"{arguments[2]}\" and are disconnected.", "Block")),
                kit.Tool("Block without a reason", "block", checks =>
                {
                    var length = checks.Number(days, "Days", 1, MaximumBlockDays);
                    return [Text(GameMasterBlocks.Normal), MinutesOf(length), Who(checks)];
                }, arguments => new GmConfirm($"Block {arguments[2]}?", $"Normal block for {arguments[1]} minute(s).", "Block")),
                kit.Tool("Lift the normal block", "unblock", checks => [Text(GameMasterBlocks.Normal), Who(checks)],
                    arguments => new GmConfirm($"Lift the normal block of {arguments[1]}?", "They can play again at once.", "Lift block")))));

        page.Children.Add(GmKit.Section(
            "Account sanction",
            "Marks the account of the character as sanctioned (user authority 1), or back to normal.",
            GmKit.Line(
                kit.Tool("Sanction the account", "account-block", checks => ["on", Who(checks)],
                    arguments => new GmConfirm($"Sanction the account of {arguments[1]}?", "The account is marked sanctioned.", "Sanction")),
                kit.Tool("Lift the sanction", "account-block", checks => ["off", Who(checks)],
                    arguments => new GmConfirm($"Lift the sanction of {arguments[1]}'s account?", "The account becomes a normal account again.", "Lift")))));

        page.Children.Add(MoveSection(kit, Who));

        var item = GmPickers.Item(model);
        var count = GmKit.Box("Count", 80, "1");
        page.Children.Add(GmKit.Section(
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
                }, primary: true))));

        var memo = GmKit.Lines("Memo text");
        page.Children.Add(GmKit.Section(
            "Memo",
            null,
            memo,
            GmKit.Line(kit.Tool("Send memo", "memo", checks =>
            {
                var who = Who(checks);
                return [who, MemoText(checks, memo, "The memo", true)];
            }))));

        page.Children.Add(GmKit.Section(
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
                kit.Tool("Close chat room", "remove-chat", checks => [Who(checks)], arguments => new GmConfirm($"Close the instant chat room of {arguments[0]}?", "Everyone in the room leaves it.", "Close")))));

        page.Children.Add(PenaltySection(kit, account));
        return page;
    }

    public static string Minutes(uint days) => (days * GameMasterChatBlocks.MinutesPerDay).ToString(CultureInfo.InvariantCulture);

    private static string MinutesOf(string days) =>
        uint.TryParse(days, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? Minutes(value) : "";

    private static string Text(byte value) => value.ToString(CultureInfo.InvariantCulture);

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
                model.Choose(character.Name);
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

    private static Border PenaltySection(GmKit kit, TextBox account)
    {
        var model = kit.Model;
        var code = GmKit.Box("Code", 80);
        var kinds = new ComboBox { Height = 28, MinHeight = 0, Width = 240, FontSize = Tokens.FontSizeBody, PlaceholderText = "Penalty kind" };
        var note = GmKit.Box("Note (optional)", 240);
        IReadOnlyList<PenaltyKind> known = [];
        async Task LoadAsync()
        {
            known = await model.PenaltyKindsAsync().ConfigureAwait(true);
            kinds.ItemsSource = known.Select(kind => $"{kind.Code}  {kind.Name}").ToList();
        }

        kinds.DropDownOpened += async (_, _) => await LoadAsync().ConfigureAwait(true);
        kinds.SelectionChanged += (_, _) =>
        {
            if (kinds.SelectedIndex >= 0 && kinds.SelectedIndex < known.Count)
            {
                code.Text = known[kinds.SelectedIndex].Code.ToString(CultureInfo.InvariantCulture);
            }
        };

        string Account(GmChecks checks) => checks.Text(account, "The account name");
        return GmKit.Section(
            "Account penalty",
            "Blocks the whole account from logging in; the lobby shows the penalty name and when it ends. Penalty kinds and lengths are on the Penalties tab.",
            GmKit.Line(
                GmKit.Field("Kind", kinds),
                GmKit.Field("Code", code),
                GmKit.Field("Note", note)),
            GmKit.Line(
                kit.Penalty("Penalize the account", GmPenalties.PenalizeName, checks =>
                {
                    var name = Account(checks);
                    var penalty = checks.Number(code, "The penalty code", short.MinValue, short.MaxValue);
                    var text = checks.Text(note, "The note", required: false);
                    return text.Length == 0 ? [name, penalty] : [name, penalty, text];
                }, arguments => new GmConfirm($"Penalize account {arguments[0]}?", $"Penalty {arguments[1]}: the account cannot log in until it ends.", "Penalize")),
                kit.Penalty("Release the account", GmPenalties.ReleaseName, checks => [Account(checks)],
                    arguments => new GmConfirm($"Release account {arguments[0]}?", "Its running penalties end now.", "Release"))));
    }
}
