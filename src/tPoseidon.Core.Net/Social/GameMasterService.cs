using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record GameMasterText(string Name, IReadOnlyList<string> Subjects);

public sealed record GameMasterBroadcast(ushort Opcode, byte[] Payload);

public sealed record GameMasterZoneOrder(ushort Zone, ushort OrderType, byte[] Data);

public sealed class GameMasterOutbox
{
    public List<GameMasterBroadcast> LobbyBroadcasts { get; } = new();

    public List<GameMasterText> Texts { get; } = new();

    public List<CoupleMessage> Messages { get; } = new();

    public List<CoupleMessage> DirectMessages { get; } = new();

    public List<PartyOrder> Orders { get; } = new();

    public List<GameMasterZoneOrder> ZoneOrders { get; } = new();

    public List<GameMasterLinkMessage> GameServerMessages { get; } = new();

    public List<ulong> Kicks { get; } = new();

    public List<string> Notes { get; } = new();

    public bool Disconnect { get; set; }

    public bool Refused { get; set; }
}

public sealed class GameMasterService
{
    private sealed class Session
    {
        public bool LoggedIn { get; set; }

        public byte[] Password { get; set; } = Array.Empty<byte>();

        public byte Speed { get; set; } = GameMasterFrames.NormalSpeed;

        public bool Immortal { get; set; }

        public bool Invisible { get; set; }

        public ushort Index { get; init; }
    }

    private readonly IGmAuthorityStore _authority;
    private readonly Encoding _text;
    private readonly Dictionary<(ulong CharacterId, bool Tool), Session> _sessions = new();
    private readonly object _gate = new();
    private readonly SortedDictionary<(ushort Zone, ushort ObjectId), uint> _deactivatedNpcs = new();
    private ushort _lastIndex;

    public IFriendStore? Friends { get; init; }

    public IGuildStore? Guilds { get; init; }

    public IGuildRulesStore? GuildRules { get; init; }

    public IGmBlockStore? BlockRecords { get; init; }

    public GameMasterService(IGmAuthorityStore authority, Encoding text)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public bool IsLoggedIn(ulong characterId, bool tool = false)
    {
        lock (_gate) return _sessions.TryGetValue((characterId, tool), out var session) && session.LoggedIn;
    }

    public GameMasterOutbox OpenSession(ulong characterId, bool tool = false)
    {
        var outbox = new GameMasterOutbox();
        lock (_gate)
        {
            if (_sessions.TryGetValue((characterId, tool), out var session))
            {
                LogOut(session, characterId, tool, outbox);
                return outbox;
            }
            _sessions[(characterId, tool)] = NewSession();
        }
        outbox.Notes.Add("session opened");
        return outbox;
    }

    public GameMasterOutbox? Login(ulong characterId, string characterName, ReadOnlySpan<byte> payload, string clientIp, bool tool = false, bool kickOnRefusal = true)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        ArgumentNullException.ThrowIfNull(clientIp);
        if (!GameMasterFrames.TryReadPassword(payload, out var password, out _)) return null;
        var outbox = new GameMasterOutbox();
        Session session;
        lock (_gate)
        {
            if (_sessions.TryGetValue((characterId, tool), out var existing) && !tool)
            {
                outbox.Notes.Add("login ignored: a session already exists on a game client connection");
                return outbox;
            }
            if (existing is { LoggedIn: true })
            {
                outbox.Notes.Add("login ignored: already logged in");
                return outbox;
            }
            session = existing ?? NewSession();
            _sessions[(characterId, tool)] = session;
        }

        if (!_authority.IsGmClientLoginPermitted(characterName, _text.GetString(password), clientIp))
        {
            lock (_gate) _sessions.Remove((characterId, tool));
            outbox.Refused = true;
            if (kickOnRefusal)
            {
                if (!tool) outbox.Kicks.Add(characterId);
                outbox.Disconnect = true;
            }
            outbox.Notes.Add($"login refused for '{characterName}' from {clientIp}");
            return outbox;
        }

        lock (_gate)
        {
            session.LoggedIn = true;
            session.Password = password;
        }
        outbox.Messages.Add(new CoupleMessage(characterId, GameMasterFrames.LoginOk, Array.Empty<byte>()));
        if (!tool) outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderSetGmAttribute, 0, GameMasterFrames.GmAttribute(true)));
        outbox.Notes.Add($"'{characterName}' logged in as GM from {clientIp}{(tool ? " (tool connection)" : string.Empty)}");
        return outbox;
    }

    public GameMasterOutbox GameFlag(ulong gameMaster, bool on, bool characterOnline)
    {
        var outbox = new GameMasterOutbox();
        if (!characterOnline)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.GameFlagFail, Array.Empty<byte>()));
            outbox.Notes.Add("GM flag refused: the GM character is not in the game");
            return outbox;
        }
        if (on)
        {
            lock (_gate)
            {
                if (!_sessions.TryGetValue((gameMaster, false), out var session))
                {
                    session = NewSession();
                    _sessions[(gameMaster, false)] = session;
                }
                session.LoggedIn = true;
            }
            outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderSetGmAttribute, 0, GameMasterFrames.GmAttribute(true)));
        }
        else
        {
            foreach (var order in Logout(gameMaster).Orders) outbox.Orders.Add(order);
            if (!outbox.Orders.Any(order => order.OrderType == GameMasterFrames.OrderSetGmAttribute))
            {
                outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderSetGmAttribute, 0, GameMasterFrames.GmAttribute(false)));
            }
        }
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.GameFlagOk, Array.Empty<byte>()));
        outbox.Notes.Add($"GM flag {(on ? "set" : "cleared")} for the GM character");
        return outbox;
    }

    public GameMasterOutbox Logout(ulong characterId, bool tool = false)
    {
        var outbox = new GameMasterOutbox();
        lock (_gate)
        {
            if (!_sessions.Remove((characterId, tool), out var session)) return outbox;
            LogOut(session, characterId, tool, outbox);
        }
        return outbox;
    }

    public GameMasterOutbox TeleportToZone(ulong gameMaster, string who, ushort zone, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(who);
        ArgumentNullException.ThrowIfNull(online);
        var outbox = new GameMasterOutbox();
        var target = who.Length == 0 ? gameMaster : online(who);
        if (target is not { } moved || zone < GameMasterFrames.FirstZone || zone >= GameMasterFrames.ZoneLimit)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.TeleportToZoneFail, GameMasterFrames.NameAndZone(who, zone, _text)));
            outbox.Notes.Add($"teleport '{who}' to zone {zone} refused");
            return outbox;
        }
        outbox.Orders.Add(new PartyOrder(moved, GameMasterFrames.OrderTeleportToZone, 0, GameMasterFrames.Zone(zone)));
        outbox.Notes.Add($"teleport 0x{moved:X} to zone {zone}");
        return outbox;
    }

    public GameMasterOutbox TeleportToChr(ulong gameMaster, string who, string to, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(who);
        ArgumentNullException.ThrowIfNull(to);
        ArgumentNullException.ThrowIfNull(online);
        var outbox = new GameMasterOutbox();
        var moved = who.Length == 0 ? gameMaster : online(who);
        var anchor = to.Length == 0 ? gameMaster : online(to);
        if (moved is not { } movedId || anchor is not { } anchorId)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.TeleportToChrFail, GameMasterFrames.TwoNames(who, to, _text)));
            outbox.Notes.Add($"teleport '{who}' to '{to}' refused");
            return outbox;
        }
        if (movedId == anchorId)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.TeleportToChrOk, GameMasterFrames.TwoNames(who, to, _text)));
            return outbox;
        }
        outbox.Orders.Add(new PartyOrder(anchorId, GameMasterFrames.OrderTeleportToChr, 0, GameMasterFrames.ChrToChr(movedId, anchorId)));
        outbox.Notes.Add($"teleport 0x{movedId:X} to 0x{anchorId:X}");
        return outbox;
    }

    public static GameMasterOutbox Kick(ulong gameMaster, string name, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(online);
        var outbox = new GameMasterOutbox();
        if (online(name) is not { } kicked)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.KickFail, Array.Empty<byte>()));
            outbox.Texts.Add(new GameMasterText(GameMasterChat.NotOnlineText, new[] { name }));
            outbox.Notes.Add($"kick '{name}' failed: not online");
            return outbox;
        }
        outbox.Kicks.Add(kicked);
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.KickOk, Array.Empty<byte>()));
        outbox.Texts.Add(new GameMasterText(GameMasterChat.KickDoneText, new[] { name }));
        outbox.Notes.Add($"kick 0x{kicked:X}");
        return outbox;
    }

    public GameMasterOutbox KickWithCause(ulong gameMaster, bool tool, string name, string reason, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(online);
        var outbox = new GameMasterOutbox();
        if (_text.GetByteCount(reason) > GameMasterFrames.MemoLength)
        {
            outbox.Texts.Add(new GameMasterText(GameMasterChat.KickReasonTooLongText, Array.Empty<string>()));
            outbox.Notes.Add($"kick '{name}' with cause refused: reason over {GameMasterFrames.MemoLength} bytes");
            return outbox;
        }
        if (online(name) is { } target)
        {
            outbox.DirectMessages.Add(new CoupleMessage(target, GameMasterFrames.ClientKickCause, GameMasterFrames.ClientCauseBody(reason, IndexOf(gameMaster, tool), _text)));
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.KickOk, Array.Empty<byte>()));
            outbox.Texts.Add(new GameMasterText(GameMasterChat.KickReasonText, new[] { name, reason }));
            outbox.Notes.Add($"kick with cause: cause sent to 0x{target:X}, kicked when the client confirms it");
        }
        else
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.KickFail, Array.Empty<byte>()));
            outbox.Texts.Add(new GameMasterText(GameMasterChat.NotOnlineText, new[] { name }));
            outbox.Notes.Add($"kick with cause of '{name}': not online");
        }
        SetAccountBlock(outbox, gameMaster, name, GameMasterFrames.AccountBlockOn);
        return outbox;
    }

    public GameMasterOutbox AccountBlock(ulong gameMaster, byte flag, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var outbox = new GameMasterOutbox();
        SetAccountBlock(outbox, gameMaster, name, flag);
        return outbox;
    }

    private void SetAccountBlock(GameMasterOutbox outbox, ulong gameMaster, string name, byte flag)
    {
        var authority = flag == GameMasterFrames.AccountBlockOn ? GmBlockRules.SanctionedUser : GmBlockRules.NormalUser;
        var rows = BlockRecords?.SetUserAuthority(name, authority) ?? 0;
        outbox.Messages.Add(new CoupleMessage(gameMaster, rows > 0 ? GameMasterFrames.AccountBlockOk : GameMasterFrames.AccountBlockFail, Array.Empty<byte>()));
        outbox.Notes.Add(rows > 0 ? $"account of '{name}' set to user authority {authority}" : $"account of '{name}' not changed: no such character");
    }

    public static GameMasterOutbox? PresentItem(string name, uint itemId, ushort count, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(online);
        if (itemId == 0 || count == 0) return null;
        var outbox = new GameMasterOutbox();
        if (online(name) is not { } receiver)
        {
            outbox.Notes.Add($"present item {itemId} x{count} to '{name}' refused: not online");
            return outbox;
        }
        outbox.Orders.Add(new PartyOrder(receiver, GameMasterFrames.OrderPresentItem, 0, GameMasterFrames.Present(receiver, itemId, count)));
        outbox.Notes.Add($"present item {itemId} x{count} to 0x{receiver:X}");
        return outbox;
    }

    public GameMasterOutbox? SuperNotice(ushort colour, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length == 0) return null;
        var outbox = new GameMasterOutbox();
        outbox.LobbyBroadcasts.Add(new GameMasterBroadcast(GameMasterFrames.SuperNoticeShown, GameMasterFrames.SuperNoticeShownBody(colour, text, _text)));
        outbox.Notes.Add($"super notice (colour {colour}): {text}");
        return outbox;
    }

    public static GameMasterOutbox? Acknowledged(ulong gameMaster, ushort report, ReadOnlySpan<byte> data)
    {
        if (!GameMasterFrames.TryReadAckResult(data, out var result)) return null;
        var ok = result == GameMasterFrames.AckOk;
        var (successOpcode, failureOpcode, label) = report switch
        {
            GameMasterFrames.ReportSummonMonster => (GameMasterFrames.SummonMonsterOk, GameMasterFrames.SummonMonsterFail, "summon monster"),
            GameMasterFrames.ReportSetIssueItem => (GameMasterFrames.SetIssueItemOk, GameMasterFrames.SetIssueItemFail, "set issue item"),
            GameMasterFrames.ReportOxGiveItem => (GameMasterFrames.OxGiveItemOk, GameMasterFrames.OxGiveItemFail, "OX give item"),
            GameMasterFrames.ReportSetMusic => (GameMasterFrames.SetMusicOk, GameMasterFrames.SetMusicFail, "set music"),
            GameMasterFrames.ReportRemoveObject => (GameMasterFrames.RemoveObjectOk, GameMasterFrames.RemoveObjectFail, "remove object"),
            GameMasterFrames.ReportSummonNpc => (GameMasterFrames.SummonNpcOk, GameMasterFrames.SummonNpcFail, "summon NPC"),
            _ => (default(ushort), default(ushort), string.Empty),
        };
        if (label.Length == 0) return null;
        var outbox = new GameMasterOutbox();
        var withReason = report is not (GameMasterFrames.ReportRemoveObject or GameMasterFrames.ReportSummonNpc);
        outbox.Messages.Add(new CoupleMessage(
            gameMaster,
            ok ? successOpcode : failureOpcode,
            ok || !withReason ? Array.Empty<byte>() : new[] { (byte)result }));
        outbox.Notes.Add(ok ? $"{label}: done" : $"{label}: refused (reason {result})");
        return outbox;
    }

    public static GameMasterOutbox ZoneCounts(ulong gameMaster, IEnumerable<ushort> zonesOfOnlineCharacters)
    {
        ArgumentNullException.ThrowIfNull(zonesOfOnlineCharacters);
        var outbox = new GameMasterOutbox();
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.ZoneCountsReply, GameMasterFrames.ZoneCountsBody(zonesOfOnlineCharacters)));
        outbox.Notes.Add("character count of every zone");
        return outbox;
    }

    public static GameMasterOutbox RemoveMyCampById(ulong gameMaster, ulong owner, bool ownerZoneKnown)
    {
        var outbox = new GameMasterOutbox();
        if (!ownerZoneKnown)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.RemoveMyCampUnknownZone, GameMasterFrames.BlockType(0)));
            outbox.Notes.Add($"remove My Camp of 0x{owner:X}: the character's zone is not known");
        }
        outbox.Orders.Add(new PartyOrder(owner, GameMasterFrames.OrderRemoveMyCamp, 0, GameMasterFrames.CharacterId(owner)));
        outbox.Notes.Add($"remove My Camp of 0x{owner:X}");
        return outbox;
    }

    public static GameMasterOutbox SummonNpc(ulong gameMaster, ReadOnlySpan<byte> request, System.Text.Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderSummonNpc, 0, request.ToArray()));
        outbox.Notes.Add(GameMasterFrames.TryReadSummonNpc(request, text, out var chrType, out var lines)
            ? $"summon NPC {chrType} with {lines.Count} line(s)"
            : $"summon NPC: {request.Length} B passed on");
        return outbox;
    }

    public static GameMasterOutbox CharCount(ulong gameMaster, ushort zone, Func<ushort, int> countOf)
    {
        ArgumentNullException.ThrowIfNull(countOf);
        var count = (uint)Math.Max(0, countOf(zone));
        var outbox = new GameMasterOutbox();
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.CharCountReply, GameMasterFrames.CharCountReplyBody(zone, count)));
        outbox.Notes.Add($"character count of zone {zone}: {count}");
        return outbox;
    }

    public static GameMasterOutbox CharInfo(ulong gameMaster, string name, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(online);
        var outbox = new GameMasterOutbox();
        if (online(name) is not { } target)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.CharInfoMissing, Array.Empty<byte>()));
            outbox.Notes.Add($"character info of '{name}': not online");
            return outbox;
        }
        outbox.Orders.Add(new PartyOrder(target, GameMasterFrames.OrderCharInfo, 0, GameMasterFrames.CharacterId(target)));
        outbox.Notes.Add($"character info of 0x{target:X} requested");
        return outbox;
    }

    public GameMasterOutbox? CharInfoReported(ulong gameMaster, ReadOnlySpan<byte> report, string account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (!GameMasterFrames.TryReadCharInfoReport(report, _text, out var info)) return null;
        var outbox = new GameMasterOutbox();
        if (info.Missing)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.CharInfoMissing, Array.Empty<byte>()));
            outbox.Notes.Add("character info: not found");
            return outbox;
        }
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.CharInfoReply, GameMasterFrames.CharInfoReplyBody(account, info, _text)));
        outbox.Notes.Add($"character info of 0x{info.CharacterId:X}");
        return outbox;
    }

    public static GameMasterOutbox? SummonMonster(ulong gameMaster, uint chrType, uint zone, byte count)
    {
        if (count == 0) return null;
        var outbox = new GameMasterOutbox();
        if (!GameMasterFrames.IsSummonableMonster(chrType))
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.SummonMonsterFail, new[] { (byte)GameMasterFrames.AckBadMonsterType }));
            outbox.Notes.Add($"summon monster {chrType} refused: monster type");
            return outbox;
        }
        if (zone >= GameMasterFrames.ZoneLimit)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.SummonMonsterFail, new[] { (byte)GameMasterFrames.AckBadZone }));
            outbox.Notes.Add($"summon monster {chrType} refused: zone {zone}");
            return outbox;
        }
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderSummonMonster, 0, GameMasterFrames.Summon(chrType, zone, count)));
        outbox.Notes.Add(zone == 0 ? $"summon monster {chrType} x{count}" : $"summon monster {chrType} x{count} in zone {zone}");
        return outbox;
    }

    public static GameMasterOutbox? SummonMonsterArea(ulong gameMaster, SummonAreaRequest request)
    {
        if (request.Count == 0) return null;
        var outbox = new GameMasterOutbox();
        if (!GameMasterFrames.IsSummonableMonster(request.ChrType))
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.SummonMonsterFail, new[] { (byte)GameMasterFrames.AckBadMonsterType }));
            outbox.Notes.Add($"summon monster {request.ChrType} refused: monster type");
            return outbox;
        }
        if (request.Map >= GameMasterFrames.ZoneLimit)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.SummonMonsterFail, new[] { (byte)GameMasterFrames.AckBadZone }));
            outbox.Notes.Add($"summon monster {request.ChrType} refused: map {request.Map}");
            return outbox;
        }
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderSummonMonsterArea, 0, GameMasterFrames.SummonArea(request)));
        outbox.Notes.Add($"summon monster {request.ChrType} x{request.Count} over radius {request.Radius} ({request.Anchor}, map {request.Map}, {request.X},{request.Y})");
        return outbox;
    }

    public static GameMasterOutbox RemoveOwnMonsters(ulong gameMaster)
    {
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderRemoveOwnMonsters, 0, Array.Empty<byte>()));
        outbox.Notes.Add("remove the monsters this GM spawned");
        return outbox;
    }

    public GameMasterBlocks Blocks { get; } = new();

    public int RestoreBlocks()
    {
        if (BlockRecords is not { } records) return 0;
        var active = records.ActiveBlocks();
        foreach (var block in active)
        {
            Blocks.Add(new GameMasterBlockEntry(block.Type, block.CharacterId, block.Name, (uint)Math.Max(0, block.MinutesLeft), block.From.ToLocalTime().ToString("T", System.Globalization.CultureInfo.CurrentCulture)));
        }
        return active.Count;
    }

    public GameMasterOutbox Block(ulong gameMaster, ushort type, uint minutes, string name, Func<string, ulong?> online, string time)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(online);
        ArgumentNullException.ThrowIfNull(time);
        var outbox = new GameMasterOutbox();
        if (!GameMasterBlocks.IsType(type))
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.BlockFail, GameMasterFrames.BlockType(type)));
            outbox.Notes.Add($"block {type} of '{name}' refused: not a block type");
            return outbox;
        }
        if (online(name) is { } target)
        {
            if (IsLoggedIn(target) || IsLoggedIn(target, tool: true))
            {
                outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.BlockFail, GameMasterFrames.BlockType(type)));
                outbox.Notes.Add($"block {type} of '{name}' refused: a GM");
                return outbox;
            }
            RecordBlock(outbox, gameMaster, (byte)type, minutes, target, name, time);
            return outbox;
        }
        if (OfflineTarget(type, name, outbox) is { } offline) RecordBlock(outbox, gameMaster, (byte)type, minutes, offline, name, time);
        return outbox;
    }

    public GameMasterOutbox BlockWithCause(ulong gameMaster, bool tool, string name, uint minutes, string cause, Func<string, ulong?> online, string time)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(cause);
        ArgumentNullException.ThrowIfNull(online);
        ArgumentNullException.ThrowIfNull(time);
        var outbox = new GameMasterOutbox();
        if (online(name) is not { } target)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.BlockWithCauseFail, GameMasterFrames.BlockType(GameMasterFrames.BlockWithCauseNotOnline)));
            outbox.Notes.Add($"block with cause of '{name}' refused: not online");
            return outbox;
        }
        outbox.DirectMessages.Add(new CoupleMessage(target, GameMasterFrames.ClientCause, GameMasterFrames.ClientCauseBody(cause, IndexOf(gameMaster, tool), _text)));
        RecordBlock(outbox, gameMaster, GameMasterBlocks.Normal, minutes, target, name, time);
        return outbox;
    }

    public GameMasterOutbox TimedBlockFromChat(ulong gameMaster, byte type, string name, int minutes, Func<string, ulong?> online, string time)
    {
        ArgumentNullException.ThrowIfNull(name);
        var texts = GameMasterChatBlocks.TextsOf(type);
        if (minutes == GameMasterChatBlocks.ReleaseValue)
        {
            var released = Unblock(gameMaster, type, name, online);
            if (Contains(released, GameMasterFrames.UnblockOk)) released.Texts.Add(new GameMasterText(texts.Released, new[] { name }));
            return released;
        }
        var permanent = minutes < 0;
        var blocked = Block(gameMaster, type, permanent ? GameMasterChatBlocks.PermanentMinutes : (uint)minutes, name, online, time);
        if (Contains(blocked, GameMasterFrames.BlockOk))
        {
            blocked.Texts.Add(permanent
                ? new GameMasterText(texts.Permanent, new[] { name })
                : new GameMasterText(texts.Timed, new[] { name, minutes.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        }
        return blocked;
    }

    public GameMasterOutbox NormalBlockFromChat(ulong gameMaster, string name, int days, string? reason, Func<string, ulong?> online, string time)
    {
        ArgumentNullException.ThrowIfNull(name);
        var dayText = days.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (days == GameMasterChatBlocks.ReleaseValue)
        {
            var released = Unblock(gameMaster, GameMasterBlocks.Normal, name, online);
            if (Contains(released, GameMasterFrames.UnblockOk)) released.Texts.Add(new GameMasterText(GameMasterChatBlocks.ReleaseBlockText, new[] { name }));
            return released;
        }
        var minutes = days < 0 ? GameMasterChatBlocks.PermanentMinutes : (uint)days * GameMasterChatBlocks.MinutesPerDay;
        if (reason is not null)
        {
            var withCause = BlockWithCause(gameMaster, false, name, minutes, reason, online, time);
            if (Contains(withCause, GameMasterFrames.BlockOk)) withCause.Texts.Add(new GameMasterText(GameMasterChatBlocks.BlockReasonText, new[] { name, dayText, reason }));
            return withCause;
        }
        var blocked = Block(gameMaster, GameMasterBlocks.Normal, minutes, name, online, time);
        if (Contains(blocked, GameMasterFrames.BlockOk))
        {
            blocked.Texts.Add(days < 0
                ? new GameMasterText(GameMasterChatBlocks.PermanentBlockNoReasonText, new[] { name })
                : new GameMasterText(GameMasterChatBlocks.BlockNoReasonText, new[] { name, dayText }));
        }
        return blocked;
    }

    public GameMasterOutbox BlockListFromChat(byte type, string header, Func<ulong, string?> onlineName)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(onlineName);
        var outbox = new GameMasterOutbox();
        var entries = Blocks.OfType(type);
        outbox.Texts.Add(new GameMasterText(header, Array.Empty<string>()));
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            outbox.Texts.Add(new GameMasterText(GameMasterChatBlocks.ListLineText, new[]
            {
                (i + 1).ToString(inv),
                onlineName(entry.CharacterId) ?? entry.Name,
                entry.CharacterId.ToString(inv),
                entry.Minutes.ToString(inv),
                entry.Time,
            }));
        }
        outbox.Texts.Add(new GameMasterText(GameMasterChatBlocks.ListCountText, new[] { entries.Count.ToString(inv) }));
        outbox.Notes.Add($"block list {type} in chat: {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}");
        return outbox;
    }

    public static GameMasterOutbox CommandHelp(IReadOnlyList<string> helpNames)
    {
        ArgumentNullException.ThrowIfNull(helpNames);
        var outbox = new GameMasterOutbox();
        foreach (var name in helpNames) outbox.Texts.Add(new GameMasterText(name, Array.Empty<string>()));
        outbox.Notes.Add($"command help: {helpNames.Count} line(s)");
        return outbox;
    }

    private static bool Contains(GameMasterOutbox outbox, ushort opcode) => outbox.Messages.Any(message => message.Opcode == opcode);

    public (ulong GameMaster, bool Tool, GameMasterOutbox Outbox)? CauseRead(ulong reader, ushort gameMasterIndex) =>
        ReaderConfirmed(reader, gameMasterIndex, GameMasterFrames.BlockWithCauseOk, GameMasterFrames.BlockWithCauseFail, "block with cause");

    public (ulong GameMaster, bool Tool, GameMasterOutbox Outbox)? KickCauseRead(ulong reader, ushort gameMasterIndex) =>
        ReaderConfirmed(reader, gameMasterIndex, GameMasterFrames.KickOk, GameMasterFrames.KickFail, "kick with cause");

    private (ulong GameMaster, bool Tool, GameMasterOutbox Outbox)? ReaderConfirmed(ulong reader, ushort gameMasterIndex, ushort kicked, ushort noCharacter, string what)
    {
        (ulong CharacterId, bool Tool) owner;
        lock (_gate)
        {
            var found = _sessions.FirstOrDefault(pair => pair.Value.Index == gameMasterIndex);
            if (found.Value is null) return null;
            owner = found.Key;
        }
        var outbox = new GameMasterOutbox();
        if (reader == 0)
        {
            outbox.Messages.Add(new CoupleMessage(owner.CharacterId, noCharacter, Array.Empty<byte>()));
            outbox.Notes.Add($"{what}: the reader has no character");
            return (owner.CharacterId, owner.Tool, outbox);
        }
        outbox.Kicks.Add(reader);
        outbox.Messages.Add(new CoupleMessage(owner.CharacterId, kicked, Array.Empty<byte>()));
        outbox.Notes.Add($"{what}: 0x{reader:X} read the cause and is kicked");
        return (owner.CharacterId, owner.Tool, outbox);
    }

    public GameMasterOutbox Unblock(ulong gameMaster, ushort type, string name, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(online);
        var outbox = new GameMasterOutbox();
        if (!GameMasterBlocks.IsType(type))
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.UnblockFail, GameMasterFrames.BlockType(type)));
            outbox.Notes.Add($"unblock {type} of '{name}' refused: not a block type");
            return outbox;
        }
        var target = online(name) ?? OfflineTarget(type, name, outbox);
        if (target is not { } characterId) return outbox;
        var result = BlockRecords?.UnblockCharacter(characterId, (byte)type, gameMaster);
        if (result != GmBlockRules.Ok)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.UnblockFail, GameMasterFrames.BlockType(type)));
            outbox.Notes.Add($"unblock {type} of 0x{characterId:X} refused: {(result is { } code ? $"database result {code}" : "no block records")}");
            return outbox;
        }
        Blocks.Remove((byte)type, characterId);
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.UnblockOk, GameMasterFrames.BlockType(type)));
        outbox.DirectMessages.Add(new CoupleMessage(characterId, GameMasterFrames.ClientUnblocked, GameMasterFrames.BlockType(type)));
        outbox.Notes.Add($"unblock {type} of 0x{characterId:X}");
        return outbox;
    }

    public bool BlockReleased(ulong characterId, ushort type) => GameMasterBlocks.IsType(type) && Blocks.Remove((byte)type, characterId);

    public int ClientBlocks(ulong characterId, IReadOnlyList<ushort> types, string time)
    {
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(time);
        var kept = 0;
        foreach (var type in types.Where(GameMasterBlocks.IsType))
        {
            Blocks.Add(new GameMasterBlockEntry((byte)type, characterId, string.Empty, GameMasterBlocks.UnknownMinutes, time));
            kept++;
        }
        return kept;
    }

    public GameMasterOutbox? BlockList(ulong gameMaster, ushort type, Func<ulong, string?> onlineName)
    {
        ArgumentNullException.ThrowIfNull(onlineName);
        if (!GameMasterBlocks.IsType(type)) return null;
        var outbox = new GameMasterOutbox();
        var entries = Blocks.OfType((byte)type);
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.BlockListReply, GameMasterFrames.BlockListBody(entries, onlineName, _text, withType: false)));
        outbox.Notes.Add($"block list {type}: {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}");
        return outbox;
    }

    public GameMasterOutbox BlockListAll(ulong gameMaster, Func<ulong, string?> onlineName)
    {
        ArgumentNullException.ThrowIfNull(onlineName);
        var outbox = new GameMasterOutbox();
        var entries = Blocks.Listed();
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.BlockListAllReply, GameMasterFrames.BlockListBody(entries, onlineName, _text, withType: true)));
        outbox.Notes.Add($"block list: {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")}");
        return outbox;
    }

    private ulong? OfflineTarget(ushort type, string name, GameMasterOutbox outbox)
    {
        if (type == GameMasterBlocks.Normal)
        {
            outbox.Notes.Add($"type {type} for offline '{name}': no reply");
            return null;
        }
        if (Friends?.FindCharacter(name) is not { } found)
        {
            outbox.Notes.Add($"type {type} for '{name}': no such character, no reply");
            return null;
        }
        return found.FriendCharacterId;
    }

    private void RecordBlock(GameMasterOutbox outbox, ulong gameMaster, byte type, uint minutes, ulong target, string name, string time)
    {
        Blocks.Add(new GameMasterBlockEntry(type, target, name, minutes, time));
        var result = BlockRecords?.BlockCharacter(target, type, (ushort)minutes, gameMaster);
        if (result != GmBlockRules.Ok)
        {
            Blocks.Remove(type, target);
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.BlockFail, GameMasterFrames.BlockType(type)));
            outbox.Notes.Add($"block {type} of 0x{target:X} cancelled: {(result is { } code ? $"database result {code}" : "no block records")}");
            return;
        }
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.BlockOk, GameMasterFrames.BlockType(type)));
        outbox.DirectMessages.Add(new CoupleMessage(target, GameMasterFrames.ClientBlocked, GameMasterFrames.ClientBlockedBody(type, minutes)));
        outbox.Notes.Add($"block {type} of 0x{target:X} for {minutes} minute(s)");
    }

    private ushort IndexOf(ulong gameMaster, bool tool)
    {
        lock (_gate) return _sessions.TryGetValue((gameMaster, tool), out var session) ? session.Index : (ushort)0;
    }

    private Session NewSession()
    {
        var start = _lastIndex;
        while (true)
        {
            _lastIndex = GameMasterFrames.NextIndex(_lastIndex);
            if (_lastIndex == start)
            {
                throw new InvalidOperationException("Every GM index is in use.");
            }
            if (!_sessions.Values.Any(session => session.Index == _lastIndex))
            {
                return new Session { Index = _lastIndex };
            }
        }
    }

    public static GameMasterOutbox OxGiveItem(ulong gameMaster, uint itemId, byte count)
    {
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderOxGiveItem, 0, GameMasterFrames.OxGift(itemId, count)));
        outbox.Notes.Add($"OX give item {itemId} x{count}");
        return outbox;
    }

    public static GameMasterOutbox SetIssueItem(ulong gameMaster, ushort objectId, uint itemId, ushort count)
    {
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderSetIssueItem, 0, GameMasterFrames.IssueItem(objectId, itemId, count)));
        outbox.Notes.Add($"issue item {itemId} x{count} on NPC {objectId}");
        return outbox;
    }

    public GameMasterOutbox? SetMusic(ulong gameMaster, string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length == 0) return null;
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderSetMusic, 0, GameMasterFrames.MusicName(file, _text)));
        outbox.Notes.Add($"set music '{file}'");
        return outbox;
    }

    public static GameMasterOutbox NpcDeactivate(ulong gameMaster, ushort objectId)
    {
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderNpcOnOff, 0, GameMasterFrames.NpcOnOff(false, objectId)));
        outbox.Notes.Add($"deactivate NPC {objectId}");
        return outbox;
    }

    public GameMasterOutbox NpcActivate(ulong gameMaster, ushort index)
    {
        var outbox = new GameMasterOutbox();
        (ushort Zone, ushort ObjectId)? entry = null;
        lock (_gate)
        {
            if (index < _deactivatedNpcs.Count) entry = _deactivatedNpcs.Keys.ElementAt(index);
        }
        if (entry is not { } found)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.NpcActivateFail, GameMasterFrames.NpcActivateFailure(true, index)));
            outbox.Notes.Add($"activate NPC: no deactivated NPC at index {index}");
            return outbox;
        }
        outbox.ZoneOrders.Add(new GameMasterZoneOrder(found.Zone, GameMasterFrames.OrderNpcOnOff, GameMasterFrames.NpcOnOff(true, found.ObjectId)));
        outbox.Notes.Add($"activate NPC {found.ObjectId} of zone {found.Zone}");
        return outbox;
    }

    public GameMasterOutbox NpcList(ulong gameMaster)
    {
        var outbox = new GameMasterOutbox();
        GameMasterFrames.DeactivatedNpc[] npcs;
        lock (_gate)
        {
            npcs = _deactivatedNpcs.Select(pair => new GameMasterFrames.DeactivatedNpc(pair.Key.ObjectId, pair.Key.Zone, pair.Value)).ToArray();
        }
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.NpcListReply, GameMasterFrames.NpcListBody(npcs)));
        outbox.Notes.Add($"{npcs.Length} deactivated NPC(s)");
        return outbox;
    }

    public static GameMasterOutbox NpcRefresh(ulong gameMaster, uint zone)
    {
        var outbox = new GameMasterOutbox();
        if (zone > ushort.MaxValue)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.NpcRefreshFail, GameMasterFrames.ZoneId(zone)));
            outbox.Notes.Add($"refresh NPC activation: zone {zone} refused");
            return outbox;
        }
        outbox.ZoneOrders.Add(new GameMasterZoneOrder((ushort)zone, GameMasterFrames.OrderNpcRefresh, Array.Empty<byte>()));
        outbox.Notes.Add($"refresh NPC activation of zone {zone}");
        return outbox;
    }

    public GameMasterOutbox? NpcOnOffReported(ulong gameMaster, ReadOnlySpan<byte> report)
    {
        if (!GameMasterFrames.TryReadNpcOnOffAck(report, out var ack)) return null;
        var outbox = new GameMasterOutbox();
        if (ack.Result != GameMasterFrames.AckOk)
        {
            outbox.Messages.Add(ack.On
                ? new CoupleMessage(gameMaster, GameMasterFrames.NpcActivateFail, GameMasterFrames.NpcActivateFailure(false, ack.ObjectId))
                : new CoupleMessage(gameMaster, GameMasterFrames.NpcDeactivateFail, GameMasterFrames.NpcDeactivateFailure(ack.ObjectId)));
            outbox.Notes.Add($"{(ack.On ? "activate" : "deactivate")} NPC {ack.ObjectId}: refused");
            return outbox;
        }
        var key = ((ushort)ack.Zone, ack.ObjectId);
        lock (_gate)
        {
            if (ack.On) _deactivatedNpcs.Remove(key);
            else _deactivatedNpcs[key] = ack.ChrType;
        }
        outbox.Messages.Add(new CoupleMessage(
            gameMaster,
            ack.On ? GameMasterFrames.NpcActivated : GameMasterFrames.NpcDeactivated,
            GameMasterFrames.NpcChrType(ack.ChrType)));
        outbox.Notes.Add($"{(ack.On ? "activated" : "deactivated")} NPC {ack.ObjectId} (zone {ack.Zone}, type {ack.ChrType})");
        return outbox;
    }

    public static GameMasterOutbox? NpcRefreshReported(ulong gameMaster, ushort zone, ReadOnlySpan<byte> report)
    {
        if (!GameMasterFrames.TryReadAckResult(report, out var result)) return null;
        var ok = result == GameMasterFrames.AckOk;
        var outbox = new GameMasterOutbox();
        outbox.Messages.Add(new CoupleMessage(gameMaster, ok ? GameMasterFrames.NpcRefreshed : GameMasterFrames.NpcRefreshFail, GameMasterFrames.ZoneId(zone)));
        outbox.Notes.Add(ok ? $"NPC activation of zone {zone} refreshed" : $"NPC activation refresh of zone {zone} failed");
        return outbox;
    }

    public static GameMasterOutbox RemoveObject(ulong gameMaster, ushort objectId)
    {
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderRemoveObject, 0, GameMasterFrames.ObjectId(objectId)));
        outbox.Notes.Add(objectId == 0 ? "remove all monsters" : $"remove object {objectId}");
        return outbox;
    }

    public static GameMasterOutbox? RemovePersonalShop(string name, Func<string, ulong?> online) =>
        RemoveOf(name, online, GameMasterFrames.OrderRemovePersonalShop, GameMasterChat.RemovePersonalShopText, "remove personal shop");

    public static GameMasterOutbox? RemoveMyCamp(string name, Func<string, ulong?> online) =>
        RemoveOf(name, online, GameMasterFrames.OrderRemoveMyCamp, GameMasterChat.RemoveMyCampText, "remove My Camp");

    public static GameMasterOutbox? RemoveInstantChat(ulong gameMaster, string name, Func<string, ulong?> online, Func<ulong, bool> removeRoom)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(online);
        ArgumentNullException.ThrowIfNull(removeRoom);
        if (name.Length == 0) return null;
        var outbox = new GameMasterOutbox();
        if (online(name) is not { } target)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.InstantChatRemoveFail, Array.Empty<byte>()));
            outbox.Texts.Add(new GameMasterText(GameMasterChat.NotOnlineText, new[] { name }));
            outbox.Notes.Add($"remove instant chat room of '{name}' refused: not online");
            return outbox;
        }
        if (!removeRoom(target))
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.InstantChatRemoveFail, Array.Empty<byte>()));
            outbox.Notes.Add($"remove instant chat room of 0x{target:X} refused: not in an instant chat room");
            return outbox;
        }
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.InstantChatRemoved, Array.Empty<byte>()));
        outbox.Texts.Add(new GameMasterText(GameMasterChat.RemoveInstantChatText, new[] { name }));
        outbox.Notes.Add($"remove instant chat room of 0x{target:X}");
        return outbox;
    }

    private static GameMasterOutbox? RemoveOf(string name, Func<string, ulong?> online, ushort order, string text, string note)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(online);
        if (name.Length == 0) return null;
        var outbox = new GameMasterOutbox();
        if (online(name) is not { } target)
        {
            outbox.Texts.Add(new GameMasterText(GameMasterChat.NotOnlineText, new[] { name }));
            outbox.Notes.Add($"{note} of '{name}' refused: not online");
            return outbox;
        }
        outbox.Orders.Add(new PartyOrder(target, order, 0, GameMasterFrames.CharacterId(target)));
        outbox.Texts.Add(new GameMasterText(text, new[] { name }));
        outbox.Notes.Add($"{note} of 0x{target:X}");
        return outbox;
    }

    public GameMasterOutbox OxQuestion(ulong gameMaster, string question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var outbox = new GameMasterOutbox();
        var w = new PacketWriter(question.Length * 2 + 1);
        w.WriteNulTerminated(question, _text);
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderOxQuestion, 0, w.ToArray()));
        outbox.Notes.Add("OX question");
        return outbox;
    }

    public static GameMasterOutbox OxAnswer(ulong gameMaster, byte answer)
    {
        var outbox = new GameMasterOutbox();
        outbox.Orders.Add(new PartyOrder(gameMaster, GameMasterFrames.OrderOxAnswer, 0, new[] { answer }));
        outbox.Notes.Add($"OX answer {answer}");
        return outbox;
    }

    public GameMasterOutbox Memo(string name, string text, Func<string, ulong?> online)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(online);
        var outbox = new GameMasterOutbox();
        if (text.Length == 0)
        {
            outbox.Texts.Add(new GameMasterText(GameMasterChat.MemoEmptyText, new[] { name }));
            outbox.Notes.Add($"memo for '{name}' refused: no text");
            return outbox;
        }
        if (_text.GetByteCount(text) > GameMasterFrames.MemoLength)
        {
            outbox.Texts.Add(new GameMasterText(GameMasterChat.MemoTooLongText, Array.Empty<string>()));
            outbox.Notes.Add($"memo for '{name}' refused: over {GameMasterFrames.MemoLength} bytes");
            return outbox;
        }
        if (online(name) is { } present)
        {
            outbox.DirectMessages.Add(new CoupleMessage(present, FriendFrames.GmMemoNow, FriendFrames.MemoNow(name, text, _text)));
            outbox.Texts.Add(new GameMasterText(GameMasterChat.MemoSentText, new[] { text, name }));
            outbox.Notes.Add($"memo shown at once to 0x{present:X}");
            return outbox;
        }
        if (StoreMemo(name, text) is not { } owner)
        {
            outbox.Notes.Add($"memo for '{name}' not stored: unknown character or full message box");
            return outbox;
        }
        outbox.Texts.Add(new GameMasterText(GameMasterChat.MemoSentText, new[] { text, name }));
        outbox.Notes.Add($"memo stored for 0x{owner:X}");
        return outbox;
    }

    public GameMasterOutbox GuildToZone(string guildName, ushort zone, Func<ulong, bool> isOnline)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(isOnline);
        var outbox = new GameMasterOutbox();
        if (zone < GameMasterFrames.FirstZone || zone >= GameMasterFrames.ZoneLimit)
        {
            outbox.Notes.Add($"guild '{guildName}' to zone {zone} refused: zone outside {GameMasterFrames.FirstZone}..{GameMasterFrames.ZoneLimit - 1}");
            return outbox;
        }
        if (GuildMembersOf(guildName) is not { } members)
        {
            outbox.Notes.Add($"guild '{guildName}' to zone {zone} refused: unknown guild");
            return outbox;
        }
        foreach (var member in members.Where(isOnline))
        {
            outbox.Orders.Add(new PartyOrder(member, GameMasterFrames.OrderTeleportToZone, 0, GameMasterFrames.Zone(zone)));
        }
        outbox.Notes.Add($"guild '{guildName}' to zone {zone}: {outbox.Orders.Count} online members");
        return outbox;
    }

    public GameMasterOutbox GuildToChr(string guildName, string target, Func<string, ulong?> online, Func<ulong, bool> isOnline)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(online);
        ArgumentNullException.ThrowIfNull(isOnline);
        var outbox = new GameMasterOutbox();
        if (online(target) is not { } anchor)
        {
            outbox.Notes.Add($"guild '{guildName}' to '{target}' refused: not online");
            return outbox;
        }
        if (GuildMembersOf(guildName) is not { } members)
        {
            outbox.Notes.Add($"guild '{guildName}' to '{target}' refused: unknown guild");
            return outbox;
        }
        foreach (var member in members.Where(member => member != anchor && isOnline(member)))
        {
            outbox.Orders.Add(new PartyOrder(anchor, GameMasterFrames.OrderTeleportToChr, 0, GameMasterFrames.ChrToChr(member, anchor)));
        }
        outbox.Notes.Add($"guild '{guildName}' to 0x{anchor:X}: {outbox.Orders.Count} online members");
        return outbox;
    }

    public GameMasterOutbox GuildMemoToMaster(string guildName, string text)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(text);
        var outbox = new GameMasterOutbox();
        if (!MemoFits(text, outbox)) return outbox;
        if (Guilds?.FindGuildByName(guildName) is not { } guild)
        {
            outbox.Notes.Add($"memo to the master of '{guildName}' refused: unknown guild");
            return outbox;
        }
        outbox.Notes.Add(StoreMemo(guild.MasterCharacterId, text) ? $"memo stored for 0x{guild.MasterCharacterId:X}" : "memo not stored");
        return outbox;
    }

    public GameMasterOutbox GuildMemoToAllMasters(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var outbox = new GameMasterOutbox();
        if (!MemoFits(text, outbox)) return outbox;
        if (Guilds is null)
        {
            outbox.Notes.Add("memo to the guild masters refused: no guild store");
            return outbox;
        }
        const int page = 50;
        var stored = 0;
        for (var offset = 0; ; offset += page)
        {
            var guilds = Guilds.ListGuilds(offset, page);
            foreach (var guild in guilds)
            {
                if (StoreMemo(guild.MasterCharacterId, text)) stored++;
            }
            if (guilds.Count < page) break;
        }
        outbox.Notes.Add($"memo stored for {stored} guild masters");
        return outbox;
    }

    public GameMasterOutbox GuildInfo(ulong gameMaster, string guildName, Func<ulong, string?> onlineName)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(onlineName);
        var outbox = new GameMasterOutbox();
        if (Guilds?.FindGuildByName(guildName) is not { } guild)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.GuildInfoMissing, Array.Empty<byte>()));
            outbox.Notes.Add($"guild info of '{guildName}': unknown guild");
            return outbox;
        }
        var code = (uint)guild.GuildCode;
        var online = Guilds.GetMembers(guild.GuildCode).Select(member => onlineName(member.CharacterId)).OfType<string>().ToArray();
        var master = GuildRules?.Detail(code)?.MasterName ?? onlineName(guild.MasterCharacterId) ?? string.Empty;
        var report = new GameMasterFrames.GuildInfoReport(code, guild.GuildName, (byte)guild.GuildMembers, master,
            guild.GuildCreatedAt.ToString(GameMasterFrames.GuildCreatedFormat, System.Globalization.CultureInfo.InvariantCulture),
            onlineName(guild.MasterCharacterId) is not null, online);
        outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.GuildInfoReply, GameMasterFrames.GuildInfoBody(report, _text)));
        outbox.Notes.Add($"guild info of '{guildName}': {online.Length} online member(s)");
        return outbox;
    }

    public GameMasterOutbox GuildGradeMemo(ulong gameMaster, byte grade, string guildName, string text)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(text);
        var outbox = new GameMasterOutbox();
        if (Guilds?.FindGuildByName(guildName) is not { } guild)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.GuildMemoFail, Array.Empty<byte>()));
            outbox.Notes.Add($"memo to guild '{guildName}' refused: unknown guild");
            return outbox;
        }
        if (!MemoFits(text, outbox)) return outbox;
        var stored = Guilds.GetMembers(guild.GuildCode).Where(member => member.GradeCode <= grade).Count(member => StoreMemo(member.CharacterId, text));
        outbox.Notes.Add($"memo to guild '{guildName}' grades 0..{grade}: stored for {stored} member(s)");
        return outbox;
    }

    public GameMasterOutbox GuildBlock(ulong gameMaster, ushort type, uint minutes, string guildName, string reason, Func<ulong, string?> onlineName, string time)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(onlineName);
        ArgumentNullException.ThrowIfNull(time);
        var outbox = new GameMasterOutbox();
        if (Guilds?.FindGuildByName(guildName) is not { } guild)
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.GuildBlockFail, GameMasterFrames.GuildBlockFailBody(GameMasterFrames.GuildBlockUnknownGuild, guildName, _text)));
            outbox.Notes.Add($"guild block of '{guildName}' refused: unknown guild");
            return outbox;
        }
        var online = Guilds.GetMembers(guild.GuildCode)
            .Select(member => (member.CharacterId, Name: onlineName(member.CharacterId)))
            .Where(member => member.Name is not null)
            .ToArray();
        if (online.Length == 0 || !GameMasterBlocks.IsType(type))
        {
            outbox.Messages.Add(new CoupleMessage(gameMaster, GameMasterFrames.GuildBlockFail, GameMasterFrames.GuildBlockFailBody(GameMasterFrames.GuildBlockNotLoaded, guildName, _text)));
            outbox.Notes.Add($"guild block of '{guildName}' refused: no member online or not a block type");
            return outbox;
        }
        var withMemo = reason.Length > 0 && MemoFits(reason, outbox);
        foreach (var (characterId, name) in online)
        {
            RecordBlock(outbox, gameMaster, (byte)type, minutes, characterId, name!, time);
            if (withMemo) StoreMemo(characterId, reason);
        }
        outbox.Messages.Insert(0, new CoupleMessage(gameMaster, GameMasterFrames.GuildBlockOk, GameMasterFrames.GuildName(guildName, _text)));
        outbox.Notes.Add($"guild block {type} of '{guildName}': {online.Length} online member(s) for {minutes} minute(s)");
        return outbox;
    }

    private ulong[]? GuildMembersOf(string guildName) =>
        Guilds?.FindGuildByName(guildName) is { } guild
            ? Guilds.GetMembers(guild.GuildCode).Select(member => member.CharacterId).ToArray()
            : null;

    private bool MemoFits(string text, GameMasterOutbox outbox)
    {
        if (text.Length == 0 || _text.GetByteCount(text) > GameMasterFrames.MemoLength)
        {
            outbox.Notes.Add($"memo refused: empty or over {GameMasterFrames.MemoLength} bytes");
            return false;
        }
        return true;
    }

    private bool StoreMemo(ulong characterId, string text) =>
        Friends?.SendMessage(characterId, FriendRules.GmSenderCharacterId, FriendRules.GmMessageKind, text) == true;

    private ulong? StoreMemo(string name, string text)
    {
        if (Friends?.FindCharacter(name) is not { } found) return null;
        return StoreMemo(found.FriendCharacterId, text) ? found.FriendCharacterId : null;
    }

    public GameMasterOutbox? SuperRun(ulong characterId, bool tool, byte speed)
    {
        if (speed < GameMasterFrames.NormalSpeed || speed > GameMasterFrames.MaxSpeed) return null;
        var outbox = new GameMasterOutbox();
        lock (_gate)
        {
            if (!_sessions.TryGetValue((characterId, tool), out var session)) return outbox;
            session.Speed = speed;
        }
        outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderSuperRun, 0, GameMasterFrames.SuperRun(speed)));
        outbox.Messages.Add(new CoupleMessage(characterId, GameMasterFrames.SpeedOk, Array.Empty<byte>()));
        outbox.Texts.Add(new GameMasterText(GameMasterChat.SpeedSetText, new[] { speed.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        outbox.Notes.Add($"super run speed {speed}");
        return outbox;
    }

    public GameMasterOutbox Immortal(ulong characterId, bool tool, bool on)
    {
        var outbox = new GameMasterOutbox();
        lock (_gate)
        {
            if (!_sessions.TryGetValue((characterId, tool), out var session)) return outbox;
            session.Immortal = on;
        }
        outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderImmortal, 0, GameMasterFrames.Toggle(on)));
        outbox.Messages.Add(new CoupleMessage(characterId, GameMasterFrames.ImmortalOk, Array.Empty<byte>()));
        outbox.Texts.Add(new GameMasterText(on ? GameMasterChat.ImmortalOnText : GameMasterChat.ImmortalOffText, Array.Empty<string>()));
        outbox.Notes.Add($"immortal {(on ? "on" : "off")}");
        return outbox;
    }

    public GameMasterOutbox Invisible(ulong characterId, bool tool, bool on)
    {
        var outbox = new GameMasterOutbox();
        lock (_gate)
        {
            if (!_sessions.TryGetValue((characterId, tool), out var session)) return outbox;
            session.Invisible = on;
        }
        outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderInvisible, 0, GameMasterFrames.Toggle(on)));
        outbox.Messages.Add(new CoupleMessage(characterId, GameMasterFrames.InvisibleOk, Array.Empty<byte>()));
        outbox.Texts.Add(new GameMasterText(on ? GameMasterChat.InvisibleOnText : GameMasterChat.InvisibleOffText, Array.Empty<string>()));
        outbox.Notes.Add($"invisible {(on ? "on" : "off")}");
        return outbox;
    }

    public GameMasterOutbox ZoneEntered(ulong characterId)
    {
        var outbox = new GameMasterOutbox();
        lock (_gate)
        {
            if (_sessions.TryGetValue((characterId, false), out var chat) && chat.LoggedIn)
            {
                outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderSetGmAttribute, 0, GameMasterFrames.GmAttribute(true)));
            }
            foreach (var tool in new[] { false, true })
            {
                if (!_sessions.TryGetValue((characterId, tool), out var session) || !session.LoggedIn) continue;
                AddActiveOrders(session, characterId, outbox);
            }
        }
        return outbox;
    }

    public bool TryAuthorize(ulong characterId, ReadOnlySpan<byte> payload, out int consumed, bool tool = false)
    {
        consumed = 0;
        if (!GameMasterFrames.TryReadPassword(payload, out var password, out var length)) return false;
        lock (_gate)
        {
            if (!_sessions.TryGetValue((characterId, tool), out var session) || !session.LoggedIn) return false;
            if (session.Password.Length == 0 || !session.Password.AsSpan().SequenceEqual(password)) return false;
        }
        consumed = length;
        return true;
    }

    public void Closed(ulong characterId, bool tool = false)
    {
        lock (_gate) _sessions.Remove((characterId, tool));
    }

    private static void AddActiveOrders(Session session, ulong characterId, GameMasterOutbox outbox)
    {
        if (session.Speed != GameMasterFrames.NormalSpeed)
        {
            outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderSuperRun, 0, GameMasterFrames.SuperRun(session.Speed)));
        }
        if (session.Immortal) outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderImmortal, 0, GameMasterFrames.Toggle(true)));
        if (session.Invisible) outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderInvisible, 0, GameMasterFrames.Toggle(true)));
    }

    private static void LogOut(Session session, ulong characterId, bool tool, GameMasterOutbox outbox)
    {
        outbox.Notes.Add($"logged out (login state {(session.LoggedIn ? 1 : 0)})");
        if (session.LoggedIn)
        {
            if (session.Speed != GameMasterFrames.NormalSpeed)
            {
                outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderSuperRun, 0, GameMasterFrames.SuperRun(GameMasterFrames.NormalSpeed)));
            }
            if (session.Immortal) outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderImmortal, 0, GameMasterFrames.Toggle(false)));
            if (session.Invisible) outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderInvisible, 0, GameMasterFrames.Toggle(false)));
            if (!tool) outbox.Orders.Add(new PartyOrder(characterId, GameMasterFrames.OrderSetGmAttribute, 0, GameMasterFrames.GmAttribute(false)));
        }
        session.LoggedIn = false;
        session.Password = Array.Empty<byte>();
        session.Speed = GameMasterFrames.NormalSpeed;
        session.Immortal = false;
        session.Invisible = false;
    }
}
