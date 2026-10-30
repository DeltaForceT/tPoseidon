using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed record GameMasterChatCommand(string Name, IReadOnlyList<string> Arguments);

public sealed record GameMasterChatUsage(string Name, string Arguments, string Description)
{
    public string Keyword => Name.ToLowerInvariant();
}

public sealed class GameMasterChat
{
    public const string ResultTable = "ChatStrings";
    public const string HelpTable = "GMStrings";
    public const char CommandMark = '@';

    public const string Login = "Login";
    public const string Logout = "Logout";
    public const string TeleportSelfToChr = "TeleportSelfToChr";
    public const string TeleportChrToZone = "TeleportChrToZone";
    public const string Kick = "Kick";
    public const string Speed = "Speed";
    public const string Immortal = "Immortal";
    public const string Invisible = "Invisible";
    public const string Fly = "Fly";
    public const string LearnSkill = "LearnSkill";
    public const string SetLevel = "Level";
    public const string Memo = "Memo";
    public const string RemovePlayerShop = "RmvPlayerShop";
    public const string RemoveMyCamp = "RmvMyCamp";
    public const string RemoveMyChat = "RmvMyChat";
    public const string SuperNotice = "SuperNotice";
    public const string Block = "Block";
    public const string ChatBlock = "ChatBlock";
    public const string MoveBlock = "MoveBlock";
    public const string TradeBlock = "TradeBlock";
    public const string BlockMyCamp = "BlockMyCamp";
    public const string BlockMyShop = "BlockMyShop";
    public const string BlockMyChat = "BlockMyChat";
    public const string ChatBlockList = "ChatBlockList";
    public const string MoveBlockList = "MoveBlockList";
    public const string TradeBlockList = "TradeBlockList";
    public const string SummonMonster = "SummonMonster";
    public const string Item = "Item";
    public const string CashItem = "CashItem";
    public const string CampItem = "CampItem";
    public const string Member = "Member";
    public const string CommandHelp = "CommandHelp";

    public const string GmFlagSetText = "GM_GMFlagSet";
    public const string KickDoneText = "GM_Kick";
    public const string NotOnlineText = "Alert_UserNotConnected";
    public const string KickReasonText = "GM_KickReason";
    public const string KickReasonTooLongText = "GM_KickStrLenOver";
    public const string MemoSentText = "GM_SendMemo";
    public const string MemoEmptyText = "GM_NoMemoData";
    public const string MemoTooLongText = "GM_DataSizeOver";
    public const string SpeedSetText = "GM_SpeedSet";
    public const string ImmortalOnText = "GM_ImmortalStateOn";
    public const string ImmortalOffText = "GM_ImmortalStateOff";
    public const string InvisibleOnText = "GM_InvisibleStateOn";
    public const string InvisibleOffText = "GM_InvisibleStateOff";
    public const string RemovePersonalShopText = "GM_RemovePersonalShop";
    public const string RemoveMyCampText = "GM_RemoveMyCamp";
    public const string RemoveInstantChatText = "GM_RemoveInstantChat";
    public const string FlyOnLine = "Your GM eagle flies.";
    public const string FlyOffLine = "Your GM eagle walks.";
    public const string FlyNoGmFlagLine = "The flying form was not changed: log in as GM first.";
    public const string FlyNotInGameLine = "The flying form was not changed: your character is not in the game.";
    public const string LearnSkillDoneLine = "The skill was learned.";
    public const string LearnSkillUnknownLine = "The skill was not learned: no such skill.";
    public const string LearnSkillBadLevelLine = "The skill was not learned: that level does not exist for this skill.";
    public const string LearnSkillNotOnlineLine = "The skill was not learned: the character is not in the game.";
    public const string LearnSkillNotHigherLine = "The skill was not changed: the character already has it at that level or higher.";
    public const string LearnSkillFailedLine = "The skill was not learned.";
    public const string SetLevelDoneLine = "The level was set.";
    public const string SetLevelNotOnlineLine = "The level was not changed: the character is not in the game.";
    public const string SetLevelInvalidLine = "The level was not changed: a level starts at 1.";
    public const string SetLevelLowerLine = "The level was not changed: the original has no way to lower a level, so only a higher level can be set.";
    public const string SetLevelSameLine = "The level was not changed: the character is already at that level (the highest level is 400).";
    public const string SetLevelFailedLine = "The level was not changed.";
    public const string NotAllowedLine = "You are not allowed to do that.";
    public const string BadArgumentsLine = "The command was not understood; type @commandhelp for the list.";
    public const string UnknownCommandLine = "Unknown command; type @commandhelp for the list.";
    public const string UnavailableLine = "The GM service is not available right now.";
    public const string ItemGivenLine = "The item was given.";
    public const string ItemUnknownLine = "The item was not given: no such item.";
    public const string ItemNotOnlineLine = "The item was not given: the character is not in the game.";
    public const string ItemNoRoomLine = "The item was not given: the bag has no room for it.";
    public const string CampItemGivenLine = "The My Camp item was added to the new items of the account.";
    public const string CampItemUnknownLine = "The My Camp item was not added: no such My Camp item.";
    public const string CampItemNoCharacterLine = "The My Camp item was not added: no such character.";
    public const string MemberOnLine = "The account of the character is now a member; it applies at the next login or zone change.";
    public const string MemberOffLine = "The account of the character is no longer a member; it applies at the next login or zone change.";
    public const string MemberNoCharacterLine = "The membership was not changed: no such character.";
    public const string ItemFailedLine = "The item was not given.";

    public static string FlyRefusedLine(byte reason) =>
        reason == GameMasterFrames.AckFlightNotInGame ? FlyNotInGameLine : FlyNoGmFlagLine;

    public static string SetLevelRefusedLine(byte reason) => reason switch
    {
        (byte)GameMasterFrames.AckLevelNotOnline => SetLevelNotOnlineLine,
        (byte)GameMasterFrames.AckLevelInvalid => SetLevelInvalidLine,
        (byte)GameMasterFrames.AckLevelLower => SetLevelLowerLine,
        (byte)GameMasterFrames.AckLevelSame => SetLevelSameLine,
        _ => SetLevelFailedLine,
    };

    public static string LearnSkillRefusedLine(byte reason) => reason switch
    {
        (byte)GameMasterFrames.AckLearnUnknownSkill => LearnSkillUnknownLine,
        (byte)GameMasterFrames.AckLearnBadLevel => LearnSkillBadLevelLine,
        (byte)GameMasterFrames.AckLearnNotOnline => LearnSkillNotOnlineLine,
        (byte)GameMasterFrames.AckLearnNotHigher => LearnSkillNotHigherLine,
        _ => LearnSkillFailedLine,
    };

    public static string ItemRefusedLine(byte reason) => reason switch
    {
        (byte)GameMasterFrames.AckPresentUnknownItem => ItemUnknownLine,
        (byte)GameMasterFrames.AckPresentNoReceiver or (byte)GameMasterFrames.AckPresentNotOnline => ItemNotOnlineLine,
        (byte)GameMasterFrames.AckPresentNoRoom => ItemNoRoomLine,
        _ => ItemFailedLine,
    };

    private static readonly System.Text.RegularExpressions.Regex Placeholder = new(@"%\s?(?:I64u|s|d)", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static IReadOnlyList<GameMasterChatUsage> Commands { get; } = new[]
    {
        new GameMasterChatUsage(Login, "<password>", "Log in as GM with the account's password (the GM character must be registered and GM login allowed from your address). Your class skills are replaced by the GM skills until you log out."),
        new GameMasterChatUsage(Logout, "", "Log out of GM mode."),
        new GameMasterChatUsage(TeleportSelfToChr, "[who] <to>", "Send yourself, or who, next to another online character."),
        new GameMasterChatUsage(TeleportChrToZone, "[who] <zoneId>", "Send yourself, or who, to a zone (1 to 999)."),
        new GameMasterChatUsage(Kick, "<name> [\"reason\"]", "Disconnect a character; with a reason the player reads it first and his account is marked sanctioned."),
        new GameMasterChatUsage(Speed, "<1..4>", "Set your movement speed (1 is normal)."),
        new GameMasterChatUsage(Immortal, "on|off", "Turn your immortal mode on or off."),
        new GameMasterChatUsage(Invisible, "on|off", "Turn your invisible mode on or off."),
        new GameMasterChatUsage(LearnSkill, "<skillId> <level> [name]", "Teach yourself, or the named online character, a skill at a level (1 up to the skill's maximum); no TM points are spent. A skill already held at that level or higher is left as it is."),
        new GameMasterChatUsage(SetLevel, "<level> [name]", "Raise yourself, or the named online character, to a level (up to 400). It does what that many natural level-ups do: bonus points, stat growth, full HP and MP, the level-up gifts and the client frames, and the exp starts at the beginning of the new level. A lower level is refused; the original has no way to lower a level."),
        new GameMasterChatUsage(Fly, "on|off", "Give your GM eagle its flying form (on) or its walking form with sword and shield (off)."),
        new GameMasterChatUsage(Memo, "<name> \"text\"", "Send a memo to a character."),
        new GameMasterChatUsage(RemovePlayerShop, "<name>", "Close the personal shop of an online character."),
        new GameMasterChatUsage(RemoveMyCamp, "<name>", "Close the My Camp of an online character."),
        new GameMasterChatUsage(RemoveMyChat, "<name>", "Close the instant chat room of an online character."),
        new GameMasterChatUsage(SuperNotice, "[colour] \"text\"", "Show a notice to everyone in the chat lobby."),
        new GameMasterChatUsage(Block, "<name> <days> [\"reason\"]", "Normal block for a number of days (0 lifts the block, -1 blocks for 15 days)."),
        new GameMasterChatUsage(ChatBlock, "<name> <minutes>", "Chat block in minutes (0 lifts the block, -1 blocks for 15 days)."),
        new GameMasterChatUsage(MoveBlock, "<name> <minutes>", "Move block in minutes (0 lifts the block, -1 blocks for 15 days)."),
        new GameMasterChatUsage(TradeBlock, "<name> <minutes>", "Trade block in minutes (0 lifts the block, -1 blocks for 15 days)."),
        new GameMasterChatUsage(BlockMyCamp, "<name> <minutes>", "My Camp block in minutes (0 lifts the block, -1 blocks for 15 days)."),
        new GameMasterChatUsage(BlockMyShop, "<name> <minutes>", "Personal shop block in minutes (0 lifts the block, -1 blocks for 15 days)."),
        new GameMasterChatUsage(BlockMyChat, "<name> <minutes>", "Chat room block in minutes (0 lifts the block, -1 blocks for 15 days)."),
        new GameMasterChatUsage(ChatBlockList, "", "List the chat-blocked characters."),
        new GameMasterChatUsage(MoveBlockList, "", "List the move-blocked characters."),
        new GameMasterChatUsage(TradeBlockList, "", "List the trade-blocked characters."),
        new GameMasterChatUsage(SummonMonster, "<monsterType> [count]", "Summon monsters where you stand (monster type 2000 to 3434)."),
        new GameMasterChatUsage(Item, "<itemId> [count] [name]", "Put an item in your own bag, or in the bag of the named online character (count defaults to 1)."),
        new GameMasterChatUsage(CashItem, "<itemId> [count] [hours] [name]", "Put an item in the cash box of your own account, or of the named online character; the player takes it out like a bought item. Hours is the period of a timed item (0 or left out: the shop period of the item, if it has one)."),
        new GameMasterChatUsage(CampItem, "<campItemId> [count] [name]", "Add a My Camp item (count defaults to 1, at most 100) to the new items of your own account, or of the named character's account; the player takes it from the camp tab of My Shop."),
        new GameMasterChatUsage(Member, "<name> on|off", "Give the whole account of a character the membership bonus (20 percent more walk, run and chase speed, and the PC-room EXP and TM rates) or take it away; it applies at the next login or zone change."),
        new GameMasterChatUsage(CommandHelp, "", "Print the GM command help lines of the client data."),
    };

    private static readonly Dictionary<string, string> NameByKeyword =
        Commands.ToDictionary(command => command.Keyword, command => command.Name, StringComparer.OrdinalIgnoreCase);

    private readonly IReadOnlyDictionary<string, string> _results;
    private readonly IReadOnlyDictionary<string, string> _help;

    public GameMasterChat(IReadOnlyDictionary<string, string> results, IReadOnlyDictionary<string, string>? help = null)
    {
        _results = results ?? throw new ArgumentNullException(nameof(results));
        _help = help ?? new Dictionary<string, string>();
    }

    public static IReadOnlySet<string> Keywords { get; } = new HashSet<string>(NameByKeyword.Keys, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> HelpNames => _help.Keys.ToArray();

    public static bool IsCommand(string line) => Parse(line) is not null;

    public static bool IsCommandLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = line.Trim();
        return text.Length >= 2 && text[0] == CommandMark;
    }

    public static int WordCount(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = line.Trim();
        return text.Length >= 2 && text[0] == CommandMark ? Split(text[1..]).Count : 0;
    }

    public static GameMasterChatCommand? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var text = line.Trim();
        if (text.Length < 2 || text[0] != CommandMark) return null;
        var words = Split(text[1..]);
        if (words.Count == 0 || !NameByKeyword.TryGetValue(words[0], out var name)) return null;
        return new GameMasterChatCommand(name, words.Skip(1).ToArray());
    }

    public string? Result(string name, params string[] subjects)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        if ((!_results.TryGetValue(name, out var text) && !_help.TryGetValue(name, out text)) || text.Trim().Length == 0) return null;
        var next = 0;
        var filled = Placeholder.Replace(text, _ => next < subjects.Length ? subjects[next++] : string.Empty);
        return filled.Trim();
    }

    public static bool TryParseSwitch(string word, out bool on)
    {
        ArgumentNullException.ThrowIfNull(word);
        on = word.Equals("on", StringComparison.OrdinalIgnoreCase);
        return on || word.Equals("off", StringComparison.OrdinalIgnoreCase);
    }

    public static byte[] Password(string password, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(text);
        var bytes = text.GetBytes(password);
        if (bytes.Length >= GameMasterFrames.PasswordBufferSize) return Array.Empty<byte>();
        var body = new byte[1 + bytes.Length];
        body[0] = (byte)bytes.Length;
        bytes.CopyTo(body, 1);
        return body;
    }

    private static List<string> Split(string text)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var started = false;
        foreach (var c in text)
        {
            if (c == '"')
            {
                quoted = !quoted;
                started = true;
                continue;
            }
            if (!quoted && char.IsWhiteSpace(c))
            {
                if (started) words.Add(current.ToString());
                current.Clear();
                started = false;
                continue;
            }
            current.Append(c);
            started = true;
        }
        if (started) words.Add(current.ToString());
        return words;
    }
}
