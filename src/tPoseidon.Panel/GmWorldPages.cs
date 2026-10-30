using System.Globalization;
using Avalonia.Controls;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Panel;

public static class GmWorldPage
{
    public const string TimeFormat = "yyyy-MM-dd HH:mm";
    public const int MegaphoneNoticeHeaderBytes = 19;
    public const int MegaphoneNoticeTextBytes = 200;

    public static Control Build(GmKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var model = kit.Model;

        var colour = GmKit.Box("Colour", 70, "0");
        var notice = GmKit.Lines("Notice text");
        var noticeSection = GmKit.Section(
            "Notice",
            "Shows a notice to everyone in the chat lobby. The colour is a number; 0 is the default.",
            notice,
            GmKit.Line(
                GmKit.Field("Colour", colour),
                kit.Tool("Send notice", "super-notice", checks =>
                {
                    var shade = checks.Number(colour, "The colour", 0, ushort.MaxValue);
                    return [shade, checks.Text(notice, "The notice")];
                }, arguments => new GmConfirm("Send this notice to everyone?", arguments[1], "Send"), primary: true)));

        var megaphoneHeader = GmKit.Box("Header", 120, "GM");
        var megaphoneNotice = GmKit.Lines("Megaphone notice text");
        var megaphoneSection = GmKit.Section(
            "Megaphone notice",
            "Queues an operator notice in the megaphone queue. Every connected player sees it on the megaphone bar as [header] text, after the shouts already waiting. The whole line is at most 300 bytes.",
            megaphoneNotice,
            GmKit.Line(
                GmKit.Field("Header", megaphoneHeader),
                kit.Tool("Queue notice", "megaphone-notice", checks =>
                    [checks.Text(megaphoneHeader, "The header", MegaphoneNoticeHeaderBytes), checks.Text(megaphoneNotice, "The notice", MegaphoneNoticeTextBytes)],
                    arguments => new GmConfirm("Queue this notice on the megaphone?", $"[{arguments[0]}] {arguments[1]}", "Queue"), primary: true)));

        var question = GmKit.Box("Question", 360);
        var answer = GmKit.Box("Answer", 70, "1");
        var gift = GmPickers.Item(model);
        var giftCount = GmKit.Box("Count", 70, "1");
        var oxSection = GmKit.Section(
            "OX quiz",
            "Works on the map your GM stands on. Handing out items needs an OX quiz map (47 or 218) and the items in your GM's bag.",
            GmKit.Line(
                GmKit.Field("Question", question),
                kit.Tool("Ask", "ox-question", checks => [checks.Text(question, "The question")], primary: true)),
            GmKit.Line(
                GmKit.Field("Answer (0 to 255)", answer),
                kit.Tool("Reveal the answer", "ox-answer", checks => [checks.Number(answer, "The answer", 0, byte.MaxValue)])),
            gift,
            GmKit.Line(
                GmKit.Field("Each player gets", giftCount),
                kit.Tool("Hand out to everyone on the map", "ox-give-item", checks =>
                {
                    var id = checks.Number(gift.Id, "The item id", 0, uint.MaxValue);
                    return [id, checks.Number(giftCount, "The count", 1, byte.MaxValue)];
                }, arguments => new GmConfirm("Hand out items to everyone on the map?", $"{arguments[1]} of item {arguments[0]} from your GM's bag to each player.", "Hand out"))));

        var music = GmKit.Box("File name without .mp3", 200);
        var hide = GmKit.Box("NPC object id", 110);
        var show = GmKit.Box("Position in the list", 110);
        var refresh = GmKit.Box("Zone id", 90);
        var mapSection = GmKit.Section(
            "Map tools",
            "Music and hiding NPCs work on the map your GM stands on. Hidden NPCs stay hidden until shown again or the server restarts.",
            GmKit.Line(GmKit.Field("Music (data/music)", music), kit.Tool("Play for the map", "set-music", checks => [checks.Text(music, "The music file")])),
            GmKit.Line(GmKit.Field("Hide NPC", hide), kit.Tool("Hide", "npc-deactivate", checks => [checks.Number(hide, "The NPC object id", 1, ushort.MaxValue)])),
            GmKit.Line(
                kit.Tool("List hidden NPCs", "npc-list", _ => []),
                GmKit.Field("Show hidden NPC", show),
                kit.Tool("Show", "npc-activate", checks => [checks.Number(show, "The position", 0, ushort.MaxValue)]),
                GmKit.Field("Show all hidden NPCs of a zone", refresh),
                kit.Tool("Show all", "npc-refresh", checks => [checks.Number(refresh, "The zone id", 0, uint.MaxValue)])));

        var exp = GmKit.Box("EXP %", 70, "100");
        var tm = GmKit.Box("TM %", 70, "0");
        var (expStart, expEnd, expTimes) = Schedule();
        var expSection = GmKit.Section(
            "EXP events",
            "Every player gets the extra EXP and TM EXP percent from monsters, drilling and skills between start and end. To delete an event, enter the same four values.",
            GmKit.Line(GmKit.Field("Extra EXP %", exp), GmKit.Field("Extra TM EXP %", tm)),
            expTimes,
            GmKit.Line(
                kit.Tool("Plan the event", "exp-reserve", checks => Event(checks, exp, "EXP %", tm, "TM %", expStart, expEnd), primary: true),
                kit.Tool("Delete the event", "exp-delete", checks => Event(checks, exp, "EXP %", tm, "TM %", expStart, expEnd),
                    _ => new GmConfirm("Delete this EXP event?", "Players stop getting the extra EXP when it is running.", "Delete")),
                kit.Tool("List EXP events", "exp-list", _ => [])));

        var expNow = GmKit.Box("EXP %", 70, "0");
        var tmNow = GmKit.Box("TM %", 70, "0");
        var expNowSection = GmKit.Section(
            "EXP rate now",
            "Sets the extra percentages on every game server now, until the next change; 0 and 0 turns it off.",
            GmKit.Line(
                GmKit.Field("Extra EXP %", expNow),
                GmKit.Field("Extra TM EXP %", tmNow),
                kit.Tool("Set now", "exp-now", checks => [checks.Number(expNow, "EXP %", 0, ushort.MaxValue), checks.Number(tmNow, "TM %", 0, ushort.MaxValue)], primary: true),
                kit.Tool("Show the rate set now", "exp-get", _ => [])));

        var stat = GmKit.Choice(GameMasterToolCommands.StatNames.Select(name => name.ToUpperInvariant()), 80);
        var percent = GmKit.Box("%", 70, "10");
        var (abilityStart, abilityEnd, abilityTimes) = Schedule();
        var abilitySection = GmKit.Section(
            "Ability events",
            "Raises one stat of every player by a percent of the value from his stat points between start and end. A deleted event that already runs stays applied until another event of the same stat ends, as on the original servers.",
            GmKit.Line(GmKit.Field("Stat", stat), GmKit.Field("Raise by %", percent)),
            abilityTimes,
            GmKit.Line(
                kit.Tool("Plan the event", "ability-reserve", checks => Ability(checks, stat, percent, abilityStart, abilityEnd), primary: true),
                kit.Tool("Delete the event", "ability-delete", checks => Ability(checks, stat, percent, abilityStart, abilityEnd),
                    _ => new GmConfirm("Delete this ability event?", "All four values must match the event.", "Delete")),
                kit.Tool("List ability events", "ability-list", _ => [])));

        return GmKit.Tab(
            "Announce to the server, run the OX quiz, work on the map your GM stands on, and plan EXP and ability events.",
            ("Announce", [noticeSection, megaphoneSection]),
            ("OX quiz", [oxSection]),
            ("Map", [mapSection]),
            ("Events", [expSection, expNowSection, abilitySection]));
    }

    private static string[] Ability(GmChecks checks, ComboBox stat, TextBox percent, TextBox start, TextBox end)
    {
        var name = GameMasterToolCommands.StatNames[Math.Max(0, stat.SelectedIndex)];
        var amount = checks.Number(percent, "The percent", 0, ushort.MaxValue);
        var (from, to) = Window(checks, start, end);
        return [name, amount, from, to];
    }

    private static string[] Event(GmChecks checks, TextBox first, string firstLabel, TextBox second, string secondLabel, TextBox start, TextBox end)
    {
        var one = checks.Number(first, firstLabel, 0, ushort.MaxValue);
        var two = checks.Number(second, secondLabel, 0, ushort.MaxValue);
        var (from, to) = Window(checks, start, end);
        return [one, two, from, to];
    }

    private static (string From, string To) Window(GmChecks checks, TextBox start, TextBox end)
    {
        var from = checks.Time(start, "The start");
        var to = checks.Time(end, "The end");
        checks.Require(from.Length == 0 || to.Length == 0 || long.Parse(to, CultureInfo.InvariantCulture) > long.Parse(from, CultureInfo.InvariantCulture), end, "The end must be after the start.");
        return (from, to);
    }

    private static (TextBox Start, TextBox End, Control Line) Schedule()
    {
        var now = DateTime.Now;
        var first = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Local).AddHours(1);
        var start = GmKit.Box(TimeFormat, 140, first.ToString(TimeFormat, CultureInfo.InvariantCulture));
        var end = GmKit.Box(TimeFormat, 140, first.AddHours(2).ToString(TimeFormat, CultureInfo.InvariantCulture));
        var lengths = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Margin = new Avalonia.Thickness(0, 0, 10, 6), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom };
        foreach (var (label, hours) in new[] { ("1 hour", 1), ("2 hours", 2), ("1 day", 24), ("1 week", 24 * 7) })
        {
            var chip = new Button { Content = label, Height = 24, MinHeight = 0, Padding = new Avalonia.Thickness(8, 0), FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary };
            chip.Classes.Add("flat");
            chip.Click += (_, _) =>
            {
                if (GameMasterToolCommands.TryParseTime(start.Text ?? "", out var from))
                {
                    end.Text = DateTimeOffset.FromUnixTimeSeconds(from).LocalDateTime.AddHours(hours).ToString(TimeFormat, CultureInfo.InvariantCulture);
                }
            };
            lengths.Children.Add(chip);
        }

        return (start, end, GmKit.Line(GmKit.Field("Start (local time)", start), GmKit.Field("End", end), GmKit.Field("Length from the start", lengths)));
    }
}

public static class GmGuildsPage
{
    public static Control Build(GmKit kit)
    {
        ArgumentNullException.ThrowIfNull(kit);
        var model = kit.Model;

        var guild = GmKit.Box("Guild name", 200);
        string Guild(GmChecks checks) => checks.Text(guild, "The guild name");
        var guildSection = GmKit.Section(
            "Guild",
            null,
            GmKit.Line(GmKit.Field("Guild", guild), kit.Tool("Guild info", "guild-info", checks => [Guild(checks)], primary: true)));

        var map = GmPickers.Map(model);
        var anchor = GmKit.Box("Character name", 200);
        model.CharacterChosen += chosen => anchor.Text = chosen;
        var moveSection = GmKit.Section(
            "Move the members",
            "Moves every online member of the guild.",
            map,
            GmKit.Line(kit.Tool("Send members to this map", "guild-teleport-zone", checks =>
            {
                var name = Guild(checks);
                return [name, checks.Number(map.Id, "The map id", 1, 999)];
            })),
            GmKit.Line(
                GmKit.Field("Next to", anchor),
                kit.Tool("Send members next to them", "guild-teleport", checks =>
                {
                    var name = Guild(checks);
                    return [name, checks.Text(anchor, "The character name")];
                })));

        var memo = GmKit.Lines("Memo text");
        var grade = GmKit.Box("Grade", 60, "0");
        string Memo(GmChecks checks) => checks.Text(memo, "The memo", GameMasterFrames.MemoLength);
        var memoSection = GmKit.Section(
            "Guild memos",
            null,
            memo,
            GmKit.Line(
                kit.Tool("To the guild master", "guild-memo", checks =>
                {
                    var name = Guild(checks);
                    return [name, Memo(checks)];
                }),
                GmKit.Field("Grades 0 up to", grade),
                kit.Tool("To members up to that grade", "guild-memo-grade", checks =>
                {
                    var level = checks.Number(grade, "The grade", 0, byte.MaxValue);
                    var name = Guild(checks);
                    return [level, name, Memo(checks)];
                }),
                kit.Tool("To every guild master", "guild-memo-all", checks => [Memo(checks)],
                    _ => new GmConfirm("Send this memo to every guild master?", "Every guild's master gets it.", "Send"))));

        var blockType = GmKit.Choice(GmBlocksPage.TimedBlocks.Select(block => block.Label).Append("Normal"), 150);
        var minutes = GmKit.Box("Minutes", 90, "30");
        var reason = GmKit.Box("Reason, sent as a memo (optional)", 280);
        var blockSection = GmKit.Section(
            "Block or dissolve",
            "Blocks every online member of the guild.",
            GmKit.Line(GmKit.Field("Block", blockType), GmKit.Field("Minutes", minutes), GmKit.Presets(minutes, GmBlocksPage.MinutePresets)),
            GmKit.Line(
                GmKit.Field("Reason", reason),
                kit.Tool("Block the online members", "guild-block", checks =>
                {
                    var type = blockType.SelectedIndex >= 0 && blockType.SelectedIndex < GmBlocksPage.TimedBlocks.Count
                        ? GmBlocksPage.TimedBlocks[blockType.SelectedIndex].Type
                        : GameMasterBlocks.Normal;
                    var length = checks.Number(minutes, "Minutes", 1, uint.MaxValue);
                    var name = Guild(checks);
                    var why = checks.Text(reason, "The reason", GameMasterFrames.MemoLength, required: false);
                    string[] head = [type.ToString(CultureInfo.InvariantCulture), length, name];
                    return why.Length == 0 ? head : [.. head, why];
                }, arguments => new GmConfirm($"Block the online members of {arguments[2]}?", $"For {arguments[1]} minute(s).", "Block")),
                kit.Tool("Dissolve the guild", "guild-dissolve", checks => [Guild(checks)],
                    arguments => new GmConfirm($"Dissolve {arguments[0]}?", "The guild is gone for good.", "Dissolve"))));

        var gamesSection = GmKit.Section(
            "Guild games",
            "Lets guilds make and join guild game rooms, or stops them (the room lists then refuse).",
            GmKit.Line(
                kit.Tool("Show the switch", "guild-games", _ => []),
                kit.Tool("Open guild games", "guild-games", _ => ["on"]),
                kit.Tool("Close guild games", "guild-games", _ => ["off"], _ => new GmConfirm("Close guild games?", "Guilds can no longer make or join guild game rooms.", "Close"))));

        var guildId = GmKit.Box("Guild id", 100);
        var flag = GmKit.Box("Flag (empty to read)", 140);
        var flagSection = GmKit.Section(
            "Guild game room flag",
            "Reads or sets the GM flag of the guild game room a guild made (rooms start with 1).",
            GmKit.Line(
                GmKit.Field("Guild id", guildId),
                GmKit.Field("Flag 0 to 255", flag),
                kit.Tool("Read or set", "guild-room-flag", checks =>
                {
                    var id = checks.Number(guildId, "The guild id", 0, uint.MaxValue);
                    return (flag.Text ?? "").Trim().Length == 0 ? [id] : [id, checks.Number(flag, "The flag", 0, byte.MaxValue)];
                })));

        return GmKit.Tab(
            "Look up a guild, move or message its members, block or dissolve it, and control the guild games.",
            ("Guild", [guildSection, moveSection, memoSection, blockSection]),
            ("Guild games", [gamesSection, flagSection]));
    }
}
