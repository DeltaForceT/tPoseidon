using System.Globalization;
using Avalonia.Controls;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public static class GmBlocksPage
{
    public const string AllTypes = "All types";

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
        model.CharacterChosen += chosen => name.Text = chosen;
        model.AccountChosen += chosen => account.Text = chosen;

        string Who(GmChecks checks) => checks.Text(name, "The character name");
        string MemoText(GmChecks checks, TextBox box, string label, bool required) => checks.Text(box, label, GameMasterFrames.MemoLength, required);

        var whoSection = GmKit.Section(
            "Who",
            "Pick a player on the Players tab and the names fill in here, or type them. Blocks use the character; penalties and sanctions use the account.",
            GmKit.Line(GmKit.Field("Character", name), GmKit.Field("Account", account)));

        var blockType = GmKit.Choice(TimedBlocks.Select(block => block.Label), 150);
        var blockMinutes = GmKit.Box("Minutes", 90, "30");
        byte TypeOf() => TimedBlocks[Math.Max(0, blockType.SelectedIndex)].Type;
        var timedSection = GmKit.Section(
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
                    arguments => new GmConfirm($"Lift the {TimedBlocks.First(block => Text(block.Type) == arguments[0]).Label.ToLowerInvariant()} block of {arguments[1]}?", "They can use it again at once.", "Lift block"))));

        var days = GmKit.Box("Days", 70, "1");
        var cause = GmKit.Box("Reason the player reads", 320);
        var normalSection = GmKit.Section(
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
                    arguments => new GmConfirm($"Lift the normal block of {arguments[1]}?", "They can play again at once.", "Lift block"))));

        var listed = GmKit.Choice(new[] { AllTypes }.Concat(TimedBlocks.Select(block => block.Label)).Append("Normal"), 160);
        var listsSection = GmKit.Section(
            "Block lists",
            null,
            GmKit.Line(
                GmKit.Field("Type", listed),
                kit.Tool("Show blocked characters", "block-list", _ => listed.SelectedIndex switch
                {
                    <= 0 => [],
                    var index when index <= TimedBlocks.Count => [Text(TimedBlocks[index - 1].Type)],
                    _ => [Text(GameMasterBlocks.Normal)],
                })));

        var sanctionSection = GmKit.Section(
            "Account sanction",
            "Marks the account of the character as sanctioned (user authority 1), or back to normal.",
            GmKit.Line(
                kit.Tool("Sanction the account", "account-block", checks => ["on", Who(checks)],
                    arguments => new GmConfirm($"Sanction the account of {arguments[1]}?", "The account is marked sanctioned.", "Sanction")),
                kit.Tool("Lift the sanction", "account-block", checks => ["off", Who(checks)],
                    arguments => new GmConfirm($"Lift the sanction of {arguments[1]}'s account?", "The account becomes a normal account again.", "Lift"))));

        var penaltySection = PenaltySection(kit, account);
        var (kindsSection, lengthsSection) = SetupSections(kit);

        return GmKit.Tab(
            "Silence or stop a player, sanction or penalize an account, and set up the penalty kinds and their lengths.",
            ("Who", [whoSection]),
            ("Blocks on a character", [timedSection, normalSection, listsSection]),
            ("Account", [sanctionSection, penaltySection]),
            ("Penalty setup", [kindsSection, lengthsSection]));
    }

    private static (Border Kinds, Border Lengths) SetupSections(GmKit kit)
    {
        var code = GmKit.Box("Code", 80);
        var name = GmKit.Box("Name the player sees", 240);
        var kind = GmKit.Box("Kind (optional)", 110);
        var kindsSection = GmKit.Section(
            "Penalty kinds",
            "A penalty kind has a code and the name the player sees at the lobby. Penalize accounts further up this tab.",
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
                kit.Penalty("Show kinds and running penalties", GmPenalties.ListName, _ => [])));

        var stepCode = GmKit.Box("Code", 80);
        var step = GmKit.Box("Step", 60, "1");
        var hours = GmKit.Box("Hours", 80, "72");

        var lengthsSection = GmKit.Section(
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
                })));

        return (kindsSection, lengthsSection);
    }

    public static string Minutes(uint days) => (days * GameMasterChatBlocks.MinutesPerDay).ToString(CultureInfo.InvariantCulture);

    private static string MinutesOf(string days) =>
        uint.TryParse(days, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? Minutes(value) : "";

    private static string Text(byte value) => value.ToString(CultureInfo.InvariantCulture);


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
            "Blocks the whole account from logging in; the lobby shows the penalty name and when it ends. Penalty kinds and lengths are set further down this tab.",
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
