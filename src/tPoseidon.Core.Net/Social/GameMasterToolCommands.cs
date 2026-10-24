using System.Globalization;
using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed record GameMasterToolCommand(string Name, string Arguments, string Description, string Example, bool ExpectsReply);

public sealed record GameMasterToolRequest(ushort Opcode, byte[] Body);

public static class GameMasterToolCommands
{
    public const int ClientTextCodePage = 949;

    public static IReadOnlyList<GameMasterToolCommand> All { get; } = new[]
    {
        new GameMasterToolCommand("teleport-zone", "[who] <zoneId>", "Send a character (default: the GM character) to a zone, 1 to 999.", "tposeidon gm teleport-zone Friend 23 --gm Boss", true),
        new GameMasterToolCommand("teleport", "[who] <to>", "Send a character (default: the GM character) next to another online character.", "tposeidon gm teleport Friend Other --gm Boss", true),
        new GameMasterToolCommand("kick", "<name> [reason]", "Disconnect an online character. With a reason the player sees it first and is disconnected when his client confirms it (10 seconds), and his account is marked sanctioned.", "tposeidon gm kick Friend \"spamming\" --gm Boss", true),
        new GameMasterToolCommand("account-block", "on|off <name>", "Mark the account of a character as sanctioned (user authority 1), or back to normal.", "tposeidon gm account-block on Friend --gm Boss", true),
        new GameMasterToolCommand("speed", "<1..4>", "Set the GM character's movement speed.", "tposeidon gm speed 3 --gm Boss", true),
        new GameMasterToolCommand("immortal", "on|off", "Turn the GM character's immortal mode on or off.", "tposeidon gm immortal on --gm Boss", true),
        new GameMasterToolCommand("invisible", "on|off", "Turn the GM character's invisible mode on or off.", "tposeidon gm invisible off --gm Boss", true),
        new GameMasterToolCommand("memo", "<name> <text>", "Send a memo to a character.", "tposeidon gm memo Friend \"Hello\" --gm Boss", false),
        new GameMasterToolCommand("super-notice", "<colour> <text>", "Show a notice to everyone in the chat lobby (colour is a number, 0 for the default).", "tposeidon gm super-notice 0 \"Maintenance at 8\" --gm Boss", false),
        new GameMasterToolCommand("char-count", "<zoneId>", "Count the characters in a zone.", "tposeidon gm char-count 23 --gm Boss", true),
        new GameMasterToolCommand("zone-counts", "", "List every zone with characters in it and how many.", "tposeidon gm zone-counts --gm Boss", true),
        new GameMasterToolCommand("char-info", "<name>", "Show the account, id, zone, level and money of an online character.", "tposeidon gm char-info Friend --gm Boss", true),
        new GameMasterToolCommand("present", "<name> <itemId> [count]", "Put items in an online character's bag (count defaults to 1).", "tposeidon gm present Friend 1001 5 --gm Boss", false),
        new GameMasterToolCommand("summon-monster", "<monsterType> [count]", "Summon monsters at the GM character's position (monster type 2000 to 3434; count defaults to 1).", "tposeidon gm summon-monster 2001 3 --gm Boss", true),
        new GameMasterToolCommand("issue-item", "<npcObjectId> <itemId> <count>", "Let an NPC on the GM character's map hand out an item, one per talk, up to count times (NPC function 65).", "tposeidon gm issue-item 4100 1001 10 --gm Boss", true),
        new GameMasterToolCommand("set-music", "<fileName>", "Play a music file from data/music (name without .mp3) for everyone on the GM character's map.", "tposeidon gm set-music town --gm Boss", true),
        new GameMasterToolCommand("remove-monsters", "", "Remove every monster on the GM character's map.", "tposeidon gm remove-monsters --gm Boss", true),
        new GameMasterToolCommand("remove-object", "<objectId>", "Remove one monster from the GM character's map by its object id.", "tposeidon gm remove-object 4097 --gm Boss", true),
        new GameMasterToolCommand("npc-deactivate", "<objectId>", "Hide an NPC on the GM character's map by its object id.", "tposeidon gm npc-deactivate 4100 --gm Boss", true),
        new GameMasterToolCommand("npc-activate", "<index>", "Show a hidden NPC again, by its position in npc-list.", "tposeidon gm npc-activate 0 --gm Boss", true),
        new GameMasterToolCommand("npc-list", "", "List the NPCs hidden with npc-deactivate (position, zone, NPC type).", "tposeidon gm npc-list --gm Boss", true),
        new GameMasterToolCommand("npc-refresh", "<zoneId>", "Show every hidden NPC of a zone again.", "tposeidon gm npc-refresh 23 --gm Boss", true),
        new GameMasterToolCommand("remove-shop", "<name>", "Close the personal shop of an online character.", "tposeidon gm remove-shop Friend --gm Boss", false),
        new GameMasterToolCommand("remove-camp-id", "<characterId>", "Close the My Camp of a character by its id (decimal or 0x hex).", "tposeidon gm remove-camp-id 0x10000009 --gm Boss", false),
        new GameMasterToolCommand("summon-npc", "<npcType> [line ...]", "Put an NPC (type 1000 to 1449 or 17000 and up) at the GM character's position; the lines are kept with it.", "tposeidon gm summon-npc 1001 \"Welcome\" --gm Boss", true),
        new GameMasterToolCommand("remove-camp", "<name>", "Close the My Camp of an online character.", "tposeidon gm remove-camp Friend --gm Boss", false),
        new GameMasterToolCommand("remove-chat", "<name>", "Close the instant chat room of an online character (a room member is refused as the room master would be).", "tposeidon gm remove-chat Friend --gm Boss", true),
        new GameMasterToolCommand("ox-question", "<text>", "Start an OX quiz question on the GM character's map (plays the timer sound).", "tposeidon gm ox-question \"Is the sky blue?\" --gm Boss", false),
        new GameMasterToolCommand("ox-answer", "<0..255>", "Reveal an OX quiz answer on the GM character's map (plays the answer sound).", "tposeidon gm ox-answer 1 --gm Boss", false),
        new GameMasterToolCommand("block", "<type> <minutes> <name>", "Block a character (type 1 normal, 2 chat, 3 trade, 5 move, 6 My Camp, 7 personal shop, 8 chat room); types 2 to 8 also work on an offline character. The block is saved in the database.", "tposeidon gm block 2 30 Friend --gm Boss", true),
        new GameMasterToolCommand("block-cause", "<name> <minutes> <cause>", "Normal block of an online character with a cause; the player sees the cause and is disconnected when he confirms it.", "tposeidon gm block-cause Friend 60 \"spamming\" --gm Boss", true),
        new GameMasterToolCommand("unblock", "<type> <name>", "Lift a block (types 2 to 8 also for an offline character).", "tposeidon gm unblock 2 Friend --gm Boss", true),
        new GameMasterToolCommand("block-list", "[type]", "List the blocked characters of one type, or of all listed types.", "tposeidon gm block-list 2 --gm Boss", true),
        new GameMasterToolCommand("ox-give-item", "<itemId> <count>", "On an OX quiz map (47 or 218), hand count of an item from the GM character's bag to every other character on the map.", "tposeidon gm ox-give-item 1001 1 --gm Boss", true),
        new GameMasterToolCommand("guild-teleport-zone", "<guild> <zoneId>", "Send every online member of a guild to a zone.", "tposeidon gm guild-teleport-zone Wolves 23 --gm Boss", false),
        new GameMasterToolCommand("guild-teleport", "<guild> <character>", "Send every online member of a guild next to a character.", "tposeidon gm guild-teleport Wolves Friend --gm Boss", false),
        new GameMasterToolCommand("guild-info", "<guild>", "Show a guild's id, name, master, creation date and online members.", "tposeidon gm guild-info Wolves --gm Boss", true),
        new GameMasterToolCommand("guild-memo-grade", "<grade> <guild> <text>", "Send a memo to every member of a guild whose grade is 0 up to the given grade.", "tposeidon gm guild-memo-grade 2 Wolves \"Meeting at 8\" --gm Boss", true),
        new GameMasterToolCommand("guild-block", "<type> <minutes> <guild> [reason]", "Block every online member of a guild (block types as for block); the reason is sent as a memo.", "tposeidon gm guild-block 2 30 Wolves \"spamming\" --gm Boss", true),
        new GameMasterToolCommand("guild-dissolve", "<guild>", "Dissolve a guild.", "tposeidon gm guild-dissolve Wolves --gm Boss", true),
        new GameMasterToolCommand("guild-memo", "<guild> <text>", "Send a memo to a guild's master.", "tposeidon gm guild-memo Wolves \"Meeting at 8\" --gm Boss", false),
        new GameMasterToolCommand("guild-memo-all", "<text>", "Send a memo to every guild master.", "tposeidon gm guild-memo-all \"Maintenance at 8\" --gm Boss", false),
        new GameMasterToolCommand("exp-list", "", "Show the EXP events: the one applied now, the running ones and the waiting ones.", "tposeidon gm exp-list --gm Boss", true),
        new GameMasterToolCommand("exp-reserve", "<exp%> <tm%> <start> <end>", "Plan an EXP event: from start to end every player gets exp% more EXP and tm% more TM EXP (times as 2026-10-24T20:00 local time, or Unix seconds).", "tposeidon gm exp-reserve 100 50 2026-10-24T20:00 2026-10-24T22:00 --gm Boss", true),
        new GameMasterToolCommand("exp-delete", "<exp%> <tm%> <start> <end>", "Delete a planned or running EXP event; all four values must match the event.", "tposeidon gm exp-delete 100 50 2026-10-24T20:00 2026-10-24T22:00 --gm Boss", true),
        new GameMasterToolCommand("exp-now", "<exp%> <tm%>", "Set the extra EXP and TM EXP percentages on every game server now, until the next change (0 0 turns it off).", "tposeidon gm exp-now 50 0 --gm Boss", false),
        new GameMasterToolCommand("exp-get", "", "Show the percentages last set with exp-now.", "tposeidon gm exp-get --gm Boss", true),
        new GameMasterToolCommand("guild-games", "[on|off|0..255]", "Show, or set for all three room lists, the flag that lets guilds make and join guild game rooms (0 closes them).", "tposeidon gm guild-games off --gm Boss", true),
        new GameMasterToolCommand("guild-room-flag", "<guildId> [0..255]", "Show or set the GM flag of the guild game room made by a guild (rooms start with 1; what the original servers do with it is not known).", "tposeidon gm guild-room-flag 120 0 --gm Boss", true),
        new GameMasterToolCommand("ability-list", "", "Show the ability events, running and waiting.", "tposeidon gm ability-list --gm Boss", true),
        new GameMasterToolCommand("ability-reserve", "<stat> <percent> <start> <end>", "Plan an ability event: from start to end every player's stat (ap, ac, dx, mp, ma, md, wt, da, lk, hp, dp, hv or 0..12) from his stat points is raised by percent (times as for exp-reserve).", "tposeidon gm ability-reserve hp 20 2026-10-24T20:00 2026-10-24T22:00 --gm Boss", true),
        new GameMasterToolCommand("ability-delete", "<stat> <percent> <start> <end>", "Delete a planned or running ability event; all four values must match. A deleted event that was already running stays applied, as on the original servers, until another event of the same stat ends.", "tposeidon gm ability-delete hp 20 2026-10-24T20:00 2026-10-24T22:00 --gm Boss", true),
    };

    public static IReadOnlyList<string> StatNames { get; } = new[] { "ap", "ac", "dx", "mp", "ma", "md", "wt", "da", "lk", "hp", "dp", "hv" };

    public static GameMasterToolCommand? Find(string name) =>
        All.FirstOrDefault(command => string.Equals(command.Name, name, StringComparison.Ordinal));

    public static bool TryParseCharacterId(string text, out ulong characterId)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.TryParse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out characterId)
            : ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out characterId);
    }

    public static Encoding ClientText()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(ClientTextCodePage);
    }

    public static GameMasterToolRequest? Build(string command, IReadOnlyList<string> args, Encoding text)
    {
        var w = new PacketWriter(64);
        switch (command)
        {
            case "teleport-zone" when args.Count is 1 or 2 && ushort.TryParse(args[^1], out var zone):
                w.WriteNulTerminated(args.Count == 2 ? args[0] : string.Empty, text);
                w.WriteUInt16(zone);
                return new(GameMasterFrames.TeleportToZone, w.ToArray());
            case "teleport" when args.Count is 1 or 2:
                w.WriteNulTerminated(args.Count == 2 ? args[0] : string.Empty, text);
                w.WriteNulTerminated(args[^1], text);
                return new(GameMasterFrames.TeleportToChr, w.ToArray());
            case "kick" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.KickRequest, w.ToArray());
            case "kick" when args.Count == 2:
                w.WriteNulTerminated(args[0], text);
                w.WriteNulTerminated(args[1], text);
                return new(GameMasterFrames.KickWithCause, w.ToArray());
            case "account-block" when args.Count == 2 && GameMasterChat.TryParseSwitch(args[0], out var sanctioned):
                w.WriteByte(sanctioned ? GameMasterFrames.AccountBlockOn : (byte)0);
                w.WriteNulTerminated(args[1], text);
                return new(GameMasterFrames.AccountBlock, w.ToArray());
            case "super-notice" when args.Count == 2 && ushort.TryParse(args[0], out var noticeColour):
                return new(GameMasterFrames.SuperNotice, GameMasterFrames.SuperNoticeRequest(noticeColour, args[1], text));
            case "char-count" when args.Count == 1 && ushort.TryParse(args[0], out var countedZone):
                w.WriteUInt16(countedZone);
                return new(GameMasterFrames.CharCount, w.ToArray());
            case "zone-counts" when args.Count == 0:
                return new(GameMasterFrames.ZoneCounts, Array.Empty<byte>());
            case "remove-camp-id" when args.Count == 1 && TryParseCharacterId(args[0], out var campOwner):
                return new(GameMasterFrames.RemoveMyCampById, GameMasterFrames.CharacterId(campOwner));
            case "summon-npc" when args.Count >= 1 && args.Count <= byte.MaxValue + 1 && uint.TryParse(args[0], out var npcType):
                return new(GameMasterFrames.SummonNpc, GameMasterFrames.SummonNpcRequest(npcType, args.Skip(1).ToArray(), text));
            case "char-info" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.CharInfo, w.ToArray());
            case "present" when args.Count is 2 or 3 && uint.TryParse(args[1], out var item) && (args.Count == 2 || ushort.TryParse(args[2], out _)):
                w.WriteUInt32(item);
                w.WriteUInt16(args.Count == 3 ? ushort.Parse(args[2], CultureInfo.InvariantCulture) : (ushort)1);
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.PresentItem, w.ToArray());
            case "summon-monster" when args.Count is 1 or 2 && uint.TryParse(args[0], out var monsterType) && (args.Count == 1 || byte.TryParse(args[1], out _)):
                w.WriteUInt32(monsterType);
                w.WriteUInt32(0);
                w.WriteByte(args.Count == 2 ? byte.Parse(args[1], CultureInfo.InvariantCulture) : (byte)1);
                return new(GameMasterFrames.SummonMonster, w.ToArray());
            case "issue-item" when args.Count == 3 && ushort.TryParse(args[0], out var issuer) && issuer != 0 && uint.TryParse(args[1], out var issuedItem) && ushort.TryParse(args[2], out var issuedCount):
                w.WriteBytes(GameMasterFrames.IssueItem(issuer, issuedItem, issuedCount));
                return new(GameMasterFrames.SetIssueItem, w.ToArray());
            case "set-music" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.SetMusic, w.ToArray());
            case "remove-monsters" when args.Count == 0:
                return new(GameMasterFrames.RemoveMonsters, Array.Empty<byte>());
            case "remove-object" when args.Count == 1 && ushort.TryParse(args[0], out var objectId) && objectId != 0:
                w.WriteUInt16(objectId);
                return new(GameMasterFrames.RemoveObject, w.ToArray());
            case "npc-deactivate" when args.Count == 1 && ushort.TryParse(args[0], out var hiddenNpc) && hiddenNpc != 0:
                w.WriteUInt16(hiddenNpc);
                return new(GameMasterFrames.NpcDeactivate, w.ToArray());
            case "npc-activate" when args.Count == 1 && ushort.TryParse(args[0], out var shownIndex):
                w.WriteUInt16(shownIndex);
                return new(GameMasterFrames.NpcActivate, w.ToArray());
            case "npc-list" when args.Count == 0:
                return new(GameMasterFrames.NpcList, Array.Empty<byte>());
            case "npc-refresh" when args.Count == 1 && uint.TryParse(args[0], out var refreshZone):
                w.WriteUInt32(refreshZone);
                return new(GameMasterFrames.NpcRefresh, w.ToArray());
            case "remove-shop" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.RemovePersonalShop, w.ToArray());
            case "remove-chat" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.RemoveInstantChat, w.ToArray());
            case "remove-camp" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.RemoveMyCampByName, w.ToArray());
            case "ox-question" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.OxQuestion, w.ToArray());
            case "ox-answer" when args.Count == 1 && byte.TryParse(args[0], out var answer):
                w.WriteByte(answer);
                return new(GameMasterFrames.OxAnswer, w.ToArray());
            case "block" when args.Count == 3 && ushort.TryParse(args[0], out var blockType) && uint.TryParse(args[1], out var blockMinutes):
                w.WriteUInt16(blockType);
                w.WriteUInt32(blockMinutes);
                w.WriteNulTerminated(args[2], text);
                return new(GameMasterFrames.Block, w.ToArray());
            case "block-cause" when args.Count == 3 && uint.TryParse(args[1], out var causeMinutes):
                w.WriteNulTerminated(args[0], text);
                w.WriteUInt32(causeMinutes);
                w.WriteNulTerminated(args[2], text);
                return new(GameMasterFrames.BlockWithCause, w.ToArray());
            case "unblock" when args.Count == 2 && ushort.TryParse(args[0], out var unblockType):
                w.WriteUInt16(unblockType);
                w.WriteNulTerminated(args[1], text);
                return new(GameMasterFrames.Unblock, w.ToArray());
            case "block-list" when args.Count == 1 && ushort.TryParse(args[0], out var listedType):
                w.WriteUInt16(listedType);
                return new(GameMasterFrames.BlockList, w.ToArray());
            case "block-list" when args.Count == 0:
                return new(GameMasterFrames.BlockListAll, Array.Empty<byte>());
            case "ox-give-item" when args.Count == 2 && uint.TryParse(args[0], out var giftItem) && byte.TryParse(args[1], out var giftCount) && giftCount != 0:
                w.WriteBytes(GameMasterFrames.OxGift(giftItem, giftCount));
                return new(GameMasterFrames.OxGiveItem, w.ToArray());
            case "guild-teleport-zone" when args.Count == 2 && ushort.TryParse(args[1], out var guildZone):
                w.WriteNulTerminated(args[0], text);
                w.WriteUInt16(guildZone);
                return new(GameMasterFrames.GuildToZone, w.ToArray());
            case "guild-teleport" when args.Count == 2:
                w.WriteNulTerminated(args[0], text);
                w.WriteNulTerminated(args[1], text);
                return new(GameMasterFrames.GuildToChr, w.ToArray());
            case "guild-info" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.GuildInfo, w.ToArray());
            case "guild-memo-grade" when args.Count == 3 && byte.TryParse(args[0], out var memoGrade):
                w.WriteByte(memoGrade);
                w.WriteNulTerminated(args[1], text);
                w.WriteNulTerminated(args[2], text);
                return new(GameMasterFrames.GuildGradeMemo, w.ToArray());
            case "guild-block" when args.Count is 3 or 4 && ushort.TryParse(args[0], out var guildBlockType) && uint.TryParse(args[1], out var guildBlockMinutes):
                w.WriteUInt16(guildBlockType);
                w.WriteUInt32(guildBlockMinutes);
                w.WriteNulTerminated(args[2], text);
                w.WriteNulTerminated(args.Count == 4 ? args[3] : string.Empty, text);
                return new(GameMasterFrames.GuildBlock, w.ToArray());
            case "guild-dissolve" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.GuildDissolve, w.ToArray());
            case "guild-memo" when args.Count == 2:
                w.WriteNulTerminated(args[0], text);
                w.WriteNulTerminated(args[1], text);
                return new(GameMasterFrames.GuildMemoToMaster, w.ToArray());
            case "guild-memo-all" when args.Count == 1:
                w.WriteNulTerminated(args[0], text);
                return new(GameMasterFrames.GuildMemoToAllMasters, w.ToArray());
            case "memo" when args.Count == 2:
                w.WriteNulTerminated(args[0], text);
                w.WriteNulTerminated(args[1], text);
                return new(GameMasterFrames.Memo, w.ToArray());
            case "speed" when args.Count == 1 && byte.TryParse(args[0], out var speed):
                w.WriteByte(speed);
                return new(GameMasterFrames.Speed, w.ToArray());
            case "immortal" when args.Count == 1 && GameMasterChat.TryParseSwitch(args[0], out var immortal):
                w.WriteBytes(GameMasterFrames.Toggle(immortal));
                return new(GameMasterFrames.Immortal, w.ToArray());
            case "invisible" when args.Count == 1 && GameMasterChat.TryParseSwitch(args[0], out var invisible):
                w.WriteBytes(GameMasterFrames.Toggle(invisible));
                return new(GameMasterFrames.Invisible, w.ToArray());
            case "exp-list" when args.Count == 0:
                return new(GameMasterEventFrames.ExpList, Array.Empty<byte>());
            case "exp-reserve" or "exp-delete" when args.Count == 4 && TryReadExpEvent(args, out var expEvent):
                expEvent.WriteTo(w);
                return new(command == "exp-reserve" ? GameMasterEventFrames.ExpReserve : GameMasterEventFrames.ExpDelete, w.ToArray());
            case "exp-now" when args.Count == 2 && ushort.TryParse(args[0], out var expNow) && ushort.TryParse(args[1], out var tmNow):
                return new(GameMasterEventFrames.ExpApply, GameMasterEventFrames.ExpPair(expNow, tmNow));
            case "exp-get" when args.Count == 0:
                return new(GameMasterEventFrames.ExpQuery, Array.Empty<byte>());
            case "guild-games" when args.Count == 0:
                return new(GameMasterFrames.GuildGameFlag, Array.Empty<byte>());
            case "guild-games" when args.Count == 1 && TryParseFlag(args[0], out var gamesFlag):
                w.WriteByte(gamesFlag);
                return new(GameMasterFrames.SetGuildGameFlag, w.ToArray());
            case "guild-room-flag" when args.Count is 1 or 2 && uint.TryParse(args[0], out var roomGuild) && (args.Count == 1 || byte.TryParse(args[1], out _)):
                w.WriteUInt32(roomGuild);
                if (args.Count == 1) return new(GameMasterFrames.GuildRoomFlag, w.ToArray());
                w.WriteByte(byte.Parse(args[1], CultureInfo.InvariantCulture));
                return new(GameMasterFrames.SetGuildRoomFlag, w.ToArray());
            case "ability-list" when args.Count == 0:
                return new(GameMasterEventFrames.AbilityList, Array.Empty<byte>());
            case "ability-reserve" or "ability-delete" when args.Count == 4 && TryReadAbilityEvent(args, out var abilityEvent):
                abilityEvent.WriteTo(w);
                return new(command == "ability-reserve" ? GameMasterEventFrames.AbilityReserve : GameMasterEventFrames.AbilityDelete, w.ToArray());
            default:
                return null;
        }
    }

    public static bool TryParseTime(string text, out long unixSeconds)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out unixSeconds)) return true;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local))
        {
            unixSeconds = new DateTimeOffset(local).ToUnixTimeSeconds();
            return true;
        }
        unixSeconds = 0;
        return false;
    }

    private static bool TryParseFlag(string text, out byte flag)
    {
        if (GameMasterChat.TryParseSwitch(text, out var on))
        {
            flag = on ? GuildGameRooms.RoomListOpen : (byte)0;
            return true;
        }
        return byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out flag);
    }

    public static bool TryParseStat(string text, out byte stat)
    {
        ArgumentNullException.ThrowIfNull(text);
        var named = StatNames.ToList().IndexOf(text.ToLowerInvariant());
        if (named >= 0)
        {
            stat = (byte)named;
            return true;
        }
        return byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out stat) && stat < GameMasterRates.AbilitySlots;
    }

    public static string StatName(byte stat) => stat < StatNames.Count ? StatNames[stat].ToUpperInvariant() : $"stat {stat}";

    private static bool TryReadAbilityEvent(IReadOnlyList<string> args, out GameMasterAbilityEvent entry)
    {
        entry = default;
        if (!TryParseStat(args[0], out var stat) || !ushort.TryParse(args[1], out var percent)) return false;
        if (!TryParseTime(args[2], out var startTime) || !TryParseTime(args[3], out var endTime) || endTime <= startTime) return false;
        entry = new GameMasterAbilityEvent(stat, percent, startTime, endTime);
        return true;
    }

    private static bool TryReadExpEvent(IReadOnlyList<string> args, out GameMasterExpEvent entry)
    {
        entry = default;
        if (!ushort.TryParse(args[0], out var expPercent) || !ushort.TryParse(args[1], out var tmPercent)) return false;
        if (!TryParseTime(args[2], out var startTime) || !TryParseTime(args[3], out var endTime) || endTime <= startTime) return false;
        entry = new GameMasterExpEvent(expPercent, tmPercent, startTime, endTime);
        return true;
    }
}
