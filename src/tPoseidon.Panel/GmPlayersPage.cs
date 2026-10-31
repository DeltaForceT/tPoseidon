using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Skills;
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

        var give = GmPickers.GiveItem(model);
        var item = give.Picker;
        var count = GmKit.Box("Count", 80, "1");
        var toBox = new CheckBox { Content = "Send to the cash box", IsChecked = false, VerticalAlignment = VerticalAlignment.Center };
        var hours = GmKit.Box("Hours (timed item)", 130);
        item.Id.TextChanged += async (_, _) =>
        {
            var catalog = await model.CatalogAsync().ConfigureAwait(true);
            var choice = !give.Camp && uint.TryParse(item.Id.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var chosenId) && chosenId <= int.MaxValue
                ? GmGiveItemChoice.For(catalog.Items.Find((int)chosenId))
                : GmGiveItemChoice.Unknown;
            toBox.IsEnabled = choice.BoxAvailable;
            toBox.IsChecked = choice.BoxAvailable && choice.BoxByDefault;
        };
        var hoursField = GmKit.Field("Hours", hours);
        var giveButton = kit.Tool("Give", () => GmGiveItem.CommandFor(toBox.IsChecked == true), [GmGiveItem.BagCommand, GmGiveItem.BoxCommand], checks =>
        {
            var who = Who(checks);
            var id = checks.Number(item.Id, "The item id", 0, uint.MaxValue);
            var amount = checks.Number(count, "Count", 1, ushort.MaxValue);
            var boxed = toBox.IsChecked == true;
            checks.Require(GmGiveItem.HoursValid(hours.Text ?? string.Empty), hours, "The hours must be a whole number from 0 to 65535, or empty.");
            return GmGiveItem.Arguments(who, id, amount, boxed, hours.Text ?? string.Empty);
        }, primary: true);
        GameCatalog? shown = null;
        var shownCount = 0;
        var campButton = kit.Camp("Give to My Camp", GmCamp.GiveName, checks =>
        {
            var who = Who(checks);
            var id = checks.Number(item.Id, "The My Camp item id", MyCampGifts.MinItemIndex, MyCampGifts.MaxItemIndex);
            return [who, id, checks.Number(count, "Count", MyCampGifts.MinCount, MyCampGifts.MaxCount)];
        }, arguments => gifts => GmCamp.Give(gifts, arguments[0], int.Parse(arguments[1], CultureInfo.InvariantCulture), int.Parse(arguments[2], CultureInfo.InvariantCulture)), primary: true);
        var campAllButton = kit.Camp("Give all shown", GmCamp.GiveAllName, checks =>
        {
            var who = Who(checks);
            var loaded = model.CatalogAsync();
            checks.Require(loaded.IsCompletedSuccessfully, item.Search, "The game data is still loading. Try again in a moment.");
            var text = (item.Search.Text ?? string.Empty).Trim();
            shown = loaded.IsCompletedSuccessfully ? loaded.Result : null;
            shownCount = shown?.FindCampItems(text).Count() ?? 0;
            checks.Require(shownCount > 0, item.Search, "No My Camp item matches the search.");
            return [who, text];
        }, arguments => gifts => GmCamp.GiveMatching(gifts, shown!, arguments[0], arguments[1]), arguments => new GmConfirm(string.Create(CultureInfo.InvariantCulture, $"Give {shownCount} My Camp items to the account of {arguments[0]}?"), "Every My Camp item the search shows is added once to the new items of the account. This cannot be taken back from here.", "Give all"));
        var campStockButton = kit.Camp("Show their My Camp items", GmCamp.StockName, checks => [Who(checks)], arguments => gifts => GmCamp.Stock(gifts, arguments[0]));
        void ShowMode(bool camp)
        {
            toBox.IsVisible = !camp;
            hoursField.IsVisible = !camp;
            giveButton.IsVisible = !camp;
            campButton.IsVisible = camp;
            campAllButton.IsVisible = camp;
            campStockButton.IsVisible = camp;
        }

        ShowMode(give.Camp);
        give.CampChanged += ShowMode;
        var giveSection = GmKit.Section(
            "Give item",
            "Puts the items in the bag of an online character, or in his cash box. A timed item put in the bag arrives with its timer: Hours is its period, empty is the period the item table gives it. The cash box is on for cash items; there empty Hours is the shop period of the item, and the player takes the item out of the cash box. With the My Camp category the item goes to the new My Camp items of the character's account, online or not (Give all shown adds every My Camp item the search lists, once each, in one step), and the player takes it from the camp tab of the My Shop window after the next camp is opened.",
            item,
            GmKit.Line(
                GmKit.Field("Count", count),
                toBox,
                hoursField,
                giveButton,
                campButton,
                campAllButton,
                campStockButton));

        var skill = GmPickers.Skill(model);
        var skillLevel = GmKit.Box("Level", 80, "1");
        var skillSection = GmKit.Section(
            "Teach a skill",
            "Teaches an online character the skill at the level, or raises a skill he already has. No TM points are spent and no skill card is used. A skill he already has at that level or higher is left as it is. Level 1 to the skill's maximum; 11 is mastered.",
            skill,
            GmKit.Line(
                GmKit.Field("Level", skillLevel),
                kit.Tool("Teach", "learn-skill", checks =>
                {
                    var who = Who(checks);
                    var id = checks.Number(skill.Id, "The skill id", 1, ushort.MaxValue);
                    return [who, id, checks.Number(skillLevel, "Level", 1, SkillDefinition.HighestLevel)];
                }, primary: true)));

        var levelBox = GmKit.Box("Level", 80, "50");
        var levelSection = GmKit.Section(
            "Set level",
            "Raises an online character to a level, 1 to 400. It does what that many natural level-ups do: 4 bonus points per level, the automatic stat growth, full HP and MP, the level-up gifts of every level passed, and the same frames to the client, so the client shows the new level, points and stats without a relog. The exp starts at the beginning of the new level. A lower level is refused: the original has no way to lower a level.",
            GmKit.Line(
                GmKit.Field("Level", levelBox),
                kit.Tool("Set level", "set-level", checks =>
                {
                    var who = Who(checks);
                    return [who, checks.Number(levelBox, "Level", LevelExpChart.FirstLevel, LevelExpChart.LastLevel)];
                }, arguments => new GmConfirm($"Set the level of {arguments[0]} to {arguments[1]}?", "Bonus points, stats, gifts and HP follow as for natural level-ups. It cannot be lowered again.", "Set level"), primary: true)));

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

        var membershipSection = GmKit.Section(
            "Membership",
            "Gives the whole account the original's membership bonus: 20 percent more walk, run and chase speed, and the PC-room EXP and TM rates. It starts at the account's next login or zone change. Uses the Account field of the character section above.",
            GmKit.Line(
                kit.Membership("Make a member", checks => [checks.Text(account, "The account name"), GmMembership.OnWord], primary: true),
                kit.Membership("Remove membership", checks => [checks.Text(account, "The account name"), GmMembership.OffWord]),
                kit.Membership("Show members", _ => [GmMembership.ListWord])));

        var playerName = GmKit.Box("Player account", 170);
        var playerPassword = GmKit.Box("Password", 170);
        playerPassword.PasswordChar = '●';
        var playerAgain = GmKit.Box("Same password again", 170);
        playerAgain.PasswordChar = '●';
        void ClearPasswords(GmOutcome outcome)
        {
            if (outcome.Ok)
            {
                playerPassword.Text = string.Empty;
                playerAgain.Text = string.Empty;
            }
        }

        IReadOnlyList<string> PasswordArguments(GmChecks checks)
        {
            var typed = checks.Text(playerName, "The account name");
            var password = playerPassword.Text ?? string.Empty;
            checks.Require(GmPlayerAccounts.NameProblem(typed.Trim()) is null, playerName, GmPlayerAccounts.NameProblem(typed.Trim()) ?? string.Empty);
            checks.Require(GmPlayerAccounts.PasswordProblem(password) is null, playerPassword, GmPlayerAccounts.PasswordProblem(password) ?? string.Empty);
            checks.Require(password == (playerAgain.Text ?? string.Empty), playerAgain, "The two passwords are not the same.");
            return [typed, password];
        }

        var accountsSection = GmKit.Section(
            "Player accounts",
            "The accounts players log in to the game with. Create one for each player, or set a new password for a player who forgot it. The name and password can be at most 19 bytes, the most the game client sends at login. Characters are made in the game after the first login. Works while the game servers are stopped (PostgreSQL must run).",
            GmKit.Line(
                GmKit.Field("Account", playerName),
                GmKit.Field("Password", playerPassword),
                GmKit.Field("Again", playerAgain)),
            GmKit.Line(
                kit.PlayerAccounts("Create account", GmPlayerAccounts.AddName, PasswordArguments, arguments => book => GmPlayerAccounts.Add(book, arguments[0], arguments[1]), arguments => new GmConfirm($"Create the account {arguments[0]}?", "The player logs in to the game with this name and password.", "Create"), ClearPasswords, primary: true),
                kit.PlayerAccounts("Set password", GmPlayerAccounts.PasswordName, PasswordArguments, arguments => book => GmPlayerAccounts.SetPassword(book, arguments[0], arguments[1]), arguments => new GmConfirm($"Set a new password for {arguments[0]}?", "The old password stops working at once.", "Set password"), ClearPasswords),
                kit.PlayerAccounts("Show accounts", GmPlayerAccounts.ListName, _ => [], _ => GmPlayerAccounts.List)));

        return GmKit.Tab(
            "Find a player, then look at them, move them, give them items, send a memo or remove them from the game.",
            ("Find", [findSection, whoSection]),
            ("Help or move", [MoveSection(kit, Who), giveSection, skillSection, levelSection, memoSection]),
            ("Membership", [membershipSection]),
            ("Accounts", [accountsSection]),
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
