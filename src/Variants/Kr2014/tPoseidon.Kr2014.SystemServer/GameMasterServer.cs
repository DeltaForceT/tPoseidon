using System.Collections.Concurrent;
using System.Net;
using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class GameMasterServer
{
    private sealed record Tool(ulong CharacterId, string Name);

    private sealed record PendingReply(S2SessionContext Requester, ulong GameMaster, ushort Zone = 0, bool FromChat = false, byte[]? OrderData = null);

    private static readonly TimeSpan RateTickPeriod = TimeSpan.FromMilliseconds(5000);

    private readonly GameMasterService _gameMasters;
    private readonly GameMasterChat _chat;
    private readonly IGmAuthorityStore _authority;
    private readonly ICharacterDirectory _directory;
    private readonly SocialRoom _room;
    private readonly GameServerLinks _games;
    private readonly Encoding _text;
    private readonly bool _toolsFromLoopbackOnly;
    private readonly Action<string> _log;
    private readonly ConcurrentDictionary<S2SessionContext, Tool?> _tools = new();
    private readonly ConcurrentDictionary<(ulong CharacterId, ushort Report), PendingReply> _pending = new();
    private readonly GameMasterRateControl _rates;
    private readonly GameMasterChatRouter _router;

    public Func<uint, ulong, CancellationToken, Task<int>>? GuildDissolver { get; set; }

    public GuildGameRooms? GuildRooms { get; set; }

    public Func<ulong, bool>? InstantChatRemover { get; set; }

    public Func<string, int, int, bool>? CampItemGiver
    {
        get => _router.CampItemGiver;
        set => _router.CampItemGiver = value;
    }

    public Func<string, bool, bool>? MemberSetter
    {
        get => _router.MemberSetter;
        set => _router.MemberSetter = value;
    }

    public IReadOnlySet<int>? KnownCampItems
    {
        get => _router.KnownCampItems;
        set => _router.KnownCampItems = value;
    }

    public GameMasterServer(
        GameMasterService gameMasters,
        GameMasterChat chat,
        IGmAuthorityStore authority,
        ICharacterDirectory directory,
        SocialRoom room,
        GameServerLinks games,
        IGmRateEventStore rateEvents,
        Encoding text,
        bool toolsFromLoopbackOnly,
        Action<string> log)
    {
        _rates = new GameMasterRateControl(rateEvents ?? throw new ArgumentNullException(nameof(rateEvents)));
        _gameMasters = gameMasters ?? throw new ArgumentNullException(nameof(gameMasters));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _toolsFromLoopbackOnly = toolsFromLoopbackOnly;
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _router = new GameMasterChatRouter(_gameMasters, _authority, _text, Online, OnlineName, BlockTime, () => _chat.HelpNames)
        {
            InstantChatRemover = RemoveInstantChat,
        };
    }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register(GameMasterFrames.ToolConnect, (_, _, _) => ToolConnectAsync(ctx));
        On(dispatcher, ctx, GameMasterFrames.SessionOpen, (gm, _) => _gameMasters.OpenSession(gm.CharacterId, gm.Tool));
        dispatcher.Register(GameMasterFrames.Login, (_, payload, ct) => LoginAsync(ctx, payload, ct));
        On(dispatcher, ctx, GameMasterFrames.Logout, (gm, _) => _gameMasters.Logout(gm.CharacterId, gm.Tool));
        Command(dispatcher, ctx, GameMasterFrames.TeleportToChr, (gm, p) =>
            ReadStrings(p, 2) is { } names ? _gameMasters.TeleportToChr(gm.CharacterId, names[0], names[1], Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.TeleportToZone, (gm, p) =>
            ReadNameAndZone(p) is { } target ? _gameMasters.TeleportToZone(gm.CharacterId, target.Name, target.Zone, Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.KickRequest, (gm, p) =>
            ReadStrings(p, 1) is { } names ? GameMasterService.Kick(gm.CharacterId, names[0], Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.KickWithCause, (gm, p) =>
            ReadStrings(p, 2) is { } cause ? _gameMasters.KickWithCause(gm.CharacterId, gm.Tool, cause[0], cause[1], Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.AccountBlock, (gm, p) =>
            p.Length >= 1 && ReadStrings(p[1..], 1) is { } account ? _gameMasters.AccountBlock(gm.CharacterId, p[0], account[0]) : null);
        Command(dispatcher, ctx, GameMasterFrames.SuperNotice, (_, p) =>
            GameMasterFrames.TryReadSuperNotice(p, _text, out var colour, out var notice) ? _gameMasters.SuperNotice(colour, notice) : null);
        Command(dispatcher, ctx, GameMasterFrames.MegaphoneNotice, (gm, p) =>
            gm.Tool && GameMasterFrames.TryReadMegaphoneNotice(p, _text, out var noticeHeader, out var noticeText) ? _gameMasters.MegaphoneNotice(gm.CharacterId, noticeHeader, noticeText) : null);
        Command(dispatcher, ctx, GameMasterFrames.CharCount, (gm, p) =>
            GameMasterFrames.TryReadZone(p, out var countedZone) ? GameMasterService.CharCount(gm.CharacterId, countedZone, CountInZone) : null);
        Command(dispatcher, ctx, GameMasterFrames.ZoneCounts, (gm, _) =>
            GameMasterService.ZoneCounts(gm.CharacterId, _games.Zones()));
        Command(dispatcher, ctx, GameMasterFrames.RemoveMyCampById, (gm, p) =>
            GameMasterFrames.TryReadCharacterId(p, out var campOwner) ? GameMasterService.RemoveMyCampById(gm.CharacterId, campOwner, _games.ZoneOf(campOwner) is not null) : null);
        Command(dispatcher, ctx, GameMasterFrames.SummonNpc, (gm, p) =>
            GameMasterService.SummonNpc(gm.CharacterId, p, _text));
        Command(dispatcher, ctx, GameMasterFrames.CharInfo, (gm, p) =>
            ReadStrings(p, 1) is { } subject ? GameMasterService.CharInfo(gm.CharacterId, subject[0], Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.PresentItem, (gm, p) =>
            ReadPresent(p) is { } present ? _gameMasters.PresentItem(gm.CharacterId, present.Name, present.ItemId, present.Count, present.Delivery, Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.SummonMonster, (gm, p) =>
            GameMasterFrames.TryReadSummon(p, out var chrType, out var zone, out var count) ? GameMasterService.SummonMonster(gm.CharacterId, chrType, zone, count) : null);
        Command(dispatcher, ctx, GameMasterFrames.SummonMonsterArea, (gm, p) =>
            gm.Tool && GameMasterFrames.TryReadSummonArea(p, out var area) ? GameMasterService.SummonMonsterArea(gm.CharacterId, area) : null);
        Command(dispatcher, ctx, GameMasterFrames.RemoveOwnMonsters, (gm, _) =>
            gm.Tool ? GameMasterService.RemoveOwnMonsters(gm.CharacterId) : null);
        Command(dispatcher, ctx, GameMasterFrames.GameFlag, (gm, p) =>
            gm.Tool && GameMasterFrames.TryReadToggle(p, out var flagOn) ? _gameMasters.GameFlag(gm.CharacterId, flagOn, IsOnline(gm.CharacterId)) : null);
        Command(dispatcher, ctx, GameMasterFrames.LearnSkill, (gm, p) =>
            gm.Tool && GameMasterFrames.TryReadLearnSkillRequest(p, _text, out var learner, out var learnedSkill, out var learnedLevel)
                ? GameMasterService.LearnSkill(gm.CharacterId, learner, learnedSkill, learnedLevel, Online)
                : null);
        Command(dispatcher, ctx, GameMasterFrames.SetLevel, (gm, p) =>
            gm.Tool && GameMasterFrames.TryReadSetLevelRequest(p, _text, out var levelled, out var targetLevel)
                ? GameMasterService.SetLevel(gm.CharacterId, levelled, targetLevel, Online)
                : null);
        Command(dispatcher, ctx, GameMasterFrames.Fly, (gm, p) =>
            gm.Tool && GameMasterFrames.TryReadToggle(p, out var flying) ? _gameMasters.Fly(gm.CharacterId, flying, IsOnline(gm.CharacterId)) : null);
        Command(dispatcher, ctx, GameMasterFrames.Block, (gm, p) =>
            GameMasterFrames.TryReadBlock(p, _text, out var blockType, out var blockMinutes, out var blocked)
                ? _gameMasters.Block(gm.CharacterId, blockType, blockMinutes, blocked, Online, BlockTime())
                : null);
        Command(dispatcher, ctx, GameMasterFrames.BlockWithCause, (gm, p) =>
            GameMasterFrames.TryReadBlockWithCause(p, _text, out var causeTarget, out var causeMinutes, out var cause)
                ? _gameMasters.BlockWithCause(gm.CharacterId, gm.Tool, causeTarget, causeMinutes, cause, Online, BlockTime())
                : null);
        Command(dispatcher, ctx, GameMasterFrames.Unblock, (gm, p) =>
            GameMasterFrames.TryReadUnblock(p, _text, out var unblockType, out var unblocked) ? _gameMasters.Unblock(gm.CharacterId, unblockType, unblocked, Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.BlockList, (gm, p) =>
            GameMasterFrames.TryReadZone(p, out var listedType) ? _gameMasters.BlockList(gm.CharacterId, listedType, OnlineName) : null);
        Command(dispatcher, ctx, GameMasterFrames.BlockListAll, (gm, _) => _gameMasters.BlockListAll(gm.CharacterId, OnlineName));
        Command(dispatcher, ctx, GameMasterFrames.BlockListByTime, (gm, p) =>
            p.Length >= sizeof(uint) ? _gameMasters.BlockListAll(gm.CharacterId, OnlineName) : null);
        dispatcher.Register(GameMasterFrames.ClientBlockList, (_, payload, _) =>
        {
            if (_room.Find(ctx) is { } reporter && GameMasterFrames.TryReadClientBlockList(payload.Span, out var reportedTypes))
            {
                var kept = _gameMasters.ClientBlocks(reporter.Character.CharacterId, reportedTypes, BlockTime());
                _log($"[gm] 0x{reporter.Character.CharacterId:X} reports {reportedTypes.Count} block(s), {kept} kept");
            }
            return Task.CompletedTask;
        });
        dispatcher.Register(GameMasterFrames.ClientCauseRead, (_, payload, ct) =>
            ReaderConfirmedAsync(ctx, payload, GameMasterFrames.ClientCauseRead, _gameMasters.CauseRead, ct));
        dispatcher.Register(GameMasterFrames.ClientKickCauseRead, (_, payload, ct) =>
            ReaderConfirmedAsync(ctx, payload, GameMasterFrames.ClientKickCauseRead, _gameMasters.KickCauseRead, ct));
        dispatcher.Register(GameMasterFrames.ClientBlockReleased, (_, payload, _) =>
        {
            if (_room.Find(ctx) is { } released && GameMasterFrames.TryReadZone(payload.Span, out var releasedType)
                && _gameMasters.BlockReleased(released.Character.CharacterId, releasedType))
            {
                _log($"[gm] block {releasedType} of 0x{released.Character.CharacterId:X} released by the client");
            }
            return Task.CompletedTask;
        });
        Command(dispatcher, ctx, GameMasterFrames.GuildInfo, (gm, p) =>
            ReadStrings(p, 1) is { } infoGuild ? _gameMasters.GuildInfo(gm.CharacterId, infoGuild[0], OnlineName) : null);
        Command(dispatcher, ctx, GameMasterFrames.GuildGradeMemo, (gm, p) =>
            GameMasterFrames.TryReadGuildGradeMemo(p, _text, out var memoGrade, out var memoGuild, out var memoText) ? _gameMasters.GuildGradeMemo(gm.CharacterId, memoGrade, memoGuild, memoText) : null);
        Command(dispatcher, ctx, GameMasterFrames.GuildBlock, (gm, p) =>
            GameMasterFrames.TryReadGuildBlock(p, _text, out var guildBlockType, out var guildBlockMinutes, out var blockedGuild, out var blockReason)
                ? _gameMasters.GuildBlock(gm.CharacterId, guildBlockType, guildBlockMinutes, blockedGuild, blockReason, OnlineName, BlockTime())
                : null);
        dispatcher.Register(GameMasterFrames.GuildDissolve, (_, payload, ct) => DissolveGuildAsync(ctx, payload, ct));
        Command(dispatcher, ctx, GameMasterFrames.OxGiveItem, (gm, p) =>
            GameMasterFrames.TryReadOxGift(p, out var giftItem, out var giftCount) ? GameMasterService.OxGiveItem(gm.CharacterId, giftItem, giftCount) : null);
        Command(dispatcher, ctx, GameMasterFrames.SetIssueItem, (gm, p) =>
            GameMasterFrames.TryReadIssueItem(p, out var issuer, out var issuedItem, out var issuedCount) ? GameMasterService.SetIssueItem(gm.CharacterId, issuer, issuedItem, issuedCount) : null);
        Command(dispatcher, ctx, GameMasterFrames.SetMusic, (gm, p) =>
            _gameMasters.SetMusic(gm.CharacterId, GameMasterFrames.ReadMusicName(p, _text)));
        Command(dispatcher, ctx, GameMasterFrames.RemoveMonsters, (gm, _) =>
            GameMasterService.RemoveObject(gm.CharacterId, 0));
        Command(dispatcher, ctx, GameMasterFrames.RemoveObject, (gm, p) =>
            GameMasterFrames.TryReadObjectId(p, out var objectId) && objectId != 0 ? GameMasterService.RemoveObject(gm.CharacterId, objectId) : null);
        Command(dispatcher, ctx, GameMasterFrames.NpcActivate, (gm, p) =>
            GameMasterFrames.TryReadZone(p, out var offIndex) ? _gameMasters.NpcActivate(gm.CharacterId, offIndex) : null);
        Command(dispatcher, ctx, GameMasterFrames.NpcDeactivate, (gm, p) =>
            GameMasterFrames.TryReadObjectId(p, out var npcId) ? GameMasterService.NpcDeactivate(gm.CharacterId, npcId) : null);
        Command(dispatcher, ctx, GameMasterFrames.NpcList, (gm, _) => _gameMasters.NpcList(gm.CharacterId));
        Command(dispatcher, ctx, GameMasterFrames.NpcRefresh, (gm, p) =>
            GameMasterFrames.TryReadZoneId(p, out var refreshedZone) ? GameMasterService.NpcRefresh(gm.CharacterId, refreshedZone) : null);
        Command(dispatcher, ctx, GameMasterFrames.RemoveInstantChat, (gm, p) =>
            ReadStrings(p, 1) is { } chatOwner ? RemoveInstantChat(gm.CharacterId, chatOwner[0]) : null);
        Command(dispatcher, ctx, GameMasterFrames.RemovePersonalShop, (_, p) =>
            ReadStrings(p, 1) is { } shop ? GameMasterService.RemovePersonalShop(shop[0], Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.RemoveMyCampByName, (_, p) =>
            ReadStrings(p, 1) is { } camp ? GameMasterService.RemoveMyCamp(camp[0], Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.OxQuestion, (gm, p) =>
            ReadStrings(p, 1) is { } question ? _gameMasters.OxQuestion(gm.CharacterId, question[0]) : null);
        Command(dispatcher, ctx, GameMasterFrames.OxAnswer, (gm, p) =>
            p.Length >= 1 ? GameMasterService.OxAnswer(gm.CharacterId, p[0]) : null);
        Command(dispatcher, ctx, GameMasterFrames.GuildToZone, (_, p) =>
            ReadNameAndZone(p) is { } move ? _gameMasters.GuildToZone(move.Name, move.Zone, IsOnline) : null);
        Command(dispatcher, ctx, GameMasterFrames.GuildToChr, (_, p) =>
            ReadStrings(p, 2) is { } move ? _gameMasters.GuildToChr(move[0], move[1], Online, IsOnline) : null);
        Command(dispatcher, ctx, GameMasterFrames.GuildMemoToMaster, (_, p) =>
            ReadStrings(p, 2) is { } memo ? _gameMasters.GuildMemoToMaster(memo[0], memo[1]) : null);
        Command(dispatcher, ctx, GameMasterFrames.GuildMemoToAllMasters, (_, p) =>
            ReadStrings(p, 1) is { } memo ? _gameMasters.GuildMemoToAllMasters(memo[0]) : null);
        Command(dispatcher, ctx, GameMasterFrames.Memo, (_, p) =>
            ReadStrings(p, 2) is { } memo ? _gameMasters.Memo(memo[0], memo[1], Online) : null);
        Command(dispatcher, ctx, GameMasterFrames.Speed, (gm, p) =>
            p.Length >= 1 ? _gameMasters.SuperRun(gm.CharacterId, gm.Tool, p[0]) : null);
        Command(dispatcher, ctx, GameMasterFrames.Immortal, (gm, p) =>
            GameMasterFrames.TryReadToggle(p, out var on) ? _gameMasters.Immortal(gm.CharacterId, gm.Tool, on) : null);
        Command(dispatcher, ctx, GameMasterFrames.Invisible, (gm, p) =>
            GameMasterFrames.TryReadToggle(p, out var on) ? _gameMasters.Invisible(gm.CharacterId, gm.Tool, on) : null);
        Command(dispatcher, ctx, GameMasterEventFrames.ExpList, (gm, _) =>
            Reply(gm, GameMasterEventFrames.ExpListReply, _rates.ExpListReply(DateTime.Now), "EXP control list"));
        Command(dispatcher, ctx, GameMasterEventFrames.ExpReserve, (gm, p) =>
            GameMasterExpEvent.TryRead(p, out var reserved)
                ? Reply(gm, GameMasterEventFrames.ExpReserved, _rates.ReserveExp(reserved), $"EXP control reserved: EXP {reserved.ExpPercent}%, TM {reserved.TmPercent}%, {reserved.StartTime}..{reserved.EndTime}")
                : null);
        Command(dispatcher, ctx, GameMasterEventFrames.ExpDelete, (gm, p) =>
            GameMasterExpEvent.TryRead(p, out var deleted)
                ? Reply(gm, GameMasterEventFrames.ExpDeleted, _rates.DeleteExp(deleted), $"EXP control deleted: EXP {deleted.ExpPercent}%, TM {deleted.TmPercent}%, {deleted.StartTime}..{deleted.EndTime}")
                : null);
        Command(dispatcher, ctx, GameMasterEventFrames.ExpApply, (_, p) =>
            GameMasterEventFrames.TryReadExpPair(p, out var expNow, out var tmNow) ? ApplyNow(_rates.SetExpNow(expNow, tmNow), $"EXP {expNow}%, TM {tmNow}% applied now") : null);
        Command(dispatcher, ctx, GameMasterEventFrames.ExpQuery, (gm, _) =>
            Reply(gm, GameMasterEventFrames.ExpQueryReply, _rates.ExpQueryReply(), "EXP rates queried"));
        Command(dispatcher, ctx, GameMasterFrames.GuildGameFlag, (gm, _) =>
            GuildRooms is { } rooms ? Reply(gm, GameMasterFrames.GuildGameFlagReply, new[] { rooms.GameOpenFlag }, $"guild game flag read: {rooms.GameOpenFlag}") : null);
        Command(dispatcher, ctx, GameMasterFrames.SetGuildGameFlag, (gm, p) =>
        {
            if (GuildRooms is not { } rooms || p.Length < 1) return null;
            rooms.SetGameOpenFlag(p[0]);
            return Reply(gm, GameMasterFrames.GuildGameFlagReply, new[] { p[0] }, $"guild game flag set to {p[0]} for the three room lists");
        });
        Command(dispatcher, ctx, GameMasterFrames.GuildRoomFlag, (gm, p) =>
        {
            if (GuildRooms is not { } rooms || p.Length < sizeof(uint)) return null;
            var roomKey = new PacketReader(p).ReadUInt32();
            var flag = rooms.GameMasterFlagOf(roomKey);
            return Reply(gm, GameMasterFrames.GuildRoomFlagReply, new[] { flag ?? 0 }, flag is { } found ? $"room {roomKey} flag read: {found}" : $"room {roomKey}: no such room");
        });
        Command(dispatcher, ctx, GameMasterFrames.SetGuildRoomFlag, (gm, p) =>
        {
            if (GuildRooms is not { } rooms || p.Length < sizeof(uint) + 1) return null;
            var roomKey = new PacketReader(p).ReadUInt32();
            var value = p[sizeof(uint)];
            var set = rooms.SetGameMasterFlag(roomKey, value);
            return Reply(gm, GameMasterFrames.GuildRoomFlagReply, new[] { value }, set ? $"room {roomKey} flag set to {value}" : $"room {roomKey}: no such room");
        });
        Command(dispatcher, ctx, GameMasterEventFrames.AbilityList, (gm, _) =>
            Reply(gm, GameMasterEventFrames.AbilityListReply, _rates.AbilityListReply(DateTime.Now), "ability control list"));
        Command(dispatcher, ctx, GameMasterEventFrames.AbilityReserve, (gm, p) =>
            GameMasterAbilityEvent.TryRead(p, out var reserved)
                ? Reply(gm, GameMasterEventFrames.AbilityReserved, _rates.ReserveAbility(reserved), $"ability control reserved: stat {reserved.Stat} +{reserved.Percent}%, {reserved.StartTime}..{reserved.EndTime}")
                : null);
        Command(dispatcher, ctx, GameMasterEventFrames.AbilityDelete, (gm, p) =>
            GameMasterAbilityEvent.TryRead(p, out var deleted)
                ? Reply(gm, GameMasterEventFrames.AbilityDeleted, _rates.DeleteAbility(deleted), $"ability control deleted: stat {deleted.Stat} +{deleted.Percent}%, {deleted.StartTime}..{deleted.EndTime}")
                : null);
    }

    public async Task RunRateEventsAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(RateTickPeriod);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                foreach (var message in _rates.Tick(DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                {
                    _log($"[gm] rate event 0x{message.Opcode:X4} to the GameServers ({message.Body.Length} B)");
                    await _games.BroadcastAsync(message.Opcode, message.Body, ct).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    public async Task GameServerLinkedAsync(S2SessionContext link, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);
        foreach (var message in _rates.LinkState())
        {
            await _games.SendToLinkAsync(link, message.Opcode, message.Body, ct).ConfigureAwait(false);
        }
    }

    private static GameMasterOutbox Reply(Caller gm, ushort opcode, byte[] body, string note)
    {
        var outbox = new GameMasterOutbox();
        outbox.Messages.Add(new CoupleMessage(gm.CharacterId, opcode, body));
        outbox.Notes.Add(note);
        return outbox;
    }

    private static GameMasterOutbox ApplyNow(GameMasterLinkMessage message, string note)
    {
        var outbox = new GameMasterOutbox();
        outbox.GameServerMessages.Add(message);
        outbox.Notes.Add(note);
        return outbox;
    }

    public Task ZoneEnteredAsync(ulong characterId, CancellationToken ct) =>
        DeliverAsync(_gameMasters.ZoneEntered(characterId), null, ct);

    public void Closed(SocialMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _gameMasters.Closed(member.Character.CharacterId);
    }

    private GameMasterOutbox? RemoveInstantChat(ulong gameMaster, string name) =>
        InstantChatRemover is { } remover ? GameMasterService.RemoveInstantChat(gameMaster, name, Online, remover) : null;

    public void Closed(S2SessionContext ctx)
    {
        if (_tools.TryRemove(ctx, out var tool) && tool is not null) _gameMasters.Closed(tool.CharacterId, tool: true);
    }

    public async Task ChatAsync(S2SessionContext link, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(link);
        if (!GameMasterFrames.TryReadChatCommand(payload.Span, _text, out var chat))
        {
            _log($"[gm] chat line from {link.Remote} dropped: {payload.Length} B is not a chat command frame");
            return;
        }
        if (_games.ZoneOf(chat.CharacterId) is null)
        {
            _log($"[gm] chat line from 0x{chat.CharacterId:X} '{chat.Name}': the character has not entered a zone on a game server yet");
        }
        if (_router.Route(chat.CharacterId, chat.Name, chat.Address, chat.Line) is not { } outcome)
        {
            _log($"[gm] chat line from 0x{chat.CharacterId:X} '{chat.Name}' ignored: not an @ command");
            return;
        }
        foreach (var entry in outcome.Audit) _log(entry);
        if (outcome.Reply is { } reply) await TellAsync(link, chat.CharacterId, reply, ct).ConfigureAwait(false);
        if (outcome.Outbox is not { } outbox) return;
        Expect(link, chat.CharacterId, outbox, fromChat: true);
        await DeliverAsync(outbox, chat.CharacterId, ct, subject: SubjectOf(chat.Line, chat.Name)).ConfigureAwait(false);
        if (outbox.Disconnect && _room.Find(chat.CharacterId) is { } member) await member.Context.Session.DisposeAsync().ConfigureAwait(false);
    }

    private static string SubjectOf(string line, string fallback) =>
        GameMasterChat.Parse(line) is { Arguments.Count: > 0 } command ? command.Arguments[0] : fallback;

    private Task TellAsync(S2SessionContext link, ulong characterId, string line, CancellationToken ct) =>
        _games.SendToLinkAsync(link, GameMasterFrames.LinkChatReply, GameMasterFrames.ChatLine(characterId, line, _text), ct);

    public async Task ReportAsync(OfficerOrder report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!_pending.TryRemove((report.CharacterId, report.OrderType), out var request)) return;
        var outbox = report.OrderType switch
        {
            GameMasterFrames.ReportCharInfo => _gameMasters.CharInfoReported(request.GameMaster, report.Data, _room.Find(report.CharacterId)?.Character.AccountLabel ?? string.Empty),
            GameMasterFrames.ReportNpcOnOff => _gameMasters.NpcOnOffReported(request.GameMaster, report.Data),
            GameMasterFrames.ReportNpcRefresh => GameMasterService.NpcRefreshReported(request.GameMaster, request.Zone, report.Data),
            GameMasterFrames.ReportSetFlight when GameMasterFrames.TryReadAckResult(report.Data, out var flightResult) =>
                GameMasterService.FlyAnswer(request.GameMaster, request.OrderData ?? Array.Empty<byte>(), flightResult),
            GameMasterFrames.ReportPresentItem when GameMasterFrames.TryReadAckResult(report.Data, out var presentResult) =>
                _gameMasters.PresentAnswer(request.GameMaster, request.OrderData ?? Array.Empty<byte>(), presentResult),
            _ => GameMasterService.Acknowledged(request.GameMaster, report.OrderType, report.Data),
        };
        if (outbox is null)
        {
            _log($"[gm] report 0x{report.OrderType:X2} for 0x{report.CharacterId:X} dropped: {report.Data.Length} B");
            return;
        }
        foreach (var note in outbox.Notes) _log($"[gm] {note}");
        if (request.FromChat) await DeliverAsync(outbox, request.GameMaster, ct).ConfigureAwait(false);
        else await DeliverAsync(outbox, null, ct, request.Requester).ConfigureAwait(false);
    }

    private void Expect(S2SessionContext requester, ulong gameMaster, GameMasterOutbox outbox, bool fromChat = false)
    {
        foreach (var order in outbox.Orders)
        {
            if (GameMasterFrames.ReportOf(order.OrderType) is { } report) _pending[(order.CharacterId, report)] = new PendingReply(requester, gameMaster, FromChat: fromChat, OrderData: order.Data);
        }
        foreach (var order in outbox.ZoneOrders)
        {
            if (GameMasterFrames.ReportOf(order.OrderType) is { } report) _pending[(0, report)] = new PendingReply(requester, gameMaster, order.Zone);
        }
    }

    private static string BlockTime() => DateTime.Now.ToString("T", System.Globalization.CultureInfo.CurrentCulture);

    private async Task ReaderConfirmedAsync(
        S2SessionContext ctx,
        ReadOnlyMemory<byte> payload,
        ushort opcode,
        Func<ulong, ushort, (ulong GameMaster, bool Tool, GameMasterOutbox Outbox)?> confirm,
        CancellationToken ct)
    {
        if (!GameMasterFrames.TryReadCauseRead(payload.Span, out var index)) return;
        var reader = _room.Find(ctx)?.Character.CharacterId ?? 0;
        if (confirm(reader, index) is not { } answer)
        {
            _log($"[gm] 0x{opcode:X4} from 0x{reader:X} dropped: no GM with index {index}");
            return;
        }
        foreach (var note in answer.Outbox.Notes) _log($"[gm] {note}");
        await DeliverAsync(answer.Outbox, null, ct, ContextOf(answer.GameMaster, answer.Tool)).ConfigureAwait(false);
    }

    private S2SessionContext? ContextOf(ulong characterId, bool tool) =>
        tool
            ? _tools.FirstOrDefault(pair => pair.Value?.CharacterId == characterId).Key
            : _room.Find(characterId)?.Context;

    private int CountInZone(ushort zone) => _games.Zones().Count(held => held == zone);

    private ulong? Online(string name)
    {
        if (_room.FindNamed(name) is { } member) return member.Character.CharacterId;
        return _directory.FindCharacterId(name) is { } id && _games.ZoneOf(id) is not null ? id : null;
    }

    private string? OnlineName(ulong characterId) => _room.Find(characterId)?.Character.Name;

    private async Task DissolveGuildAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (CallerOf(ctx) is not { } caller) return;
        if (!_gameMasters.TryAuthorize(caller.CharacterId, payload.Span, out var consumed, caller.Tool) || ReadStrings(payload.Span[consumed..], 1) is not { } names)
        {
            _log($"[gm] 0x{GameMasterFrames.GuildDissolve:X4} from 0x{caller.CharacterId:X} refused: no GM login or bad request");
            return;
        }
        var guildName = names[0];
        var error = -1;
        if (_gameMasters.Guilds?.FindGuildByName(guildName) is { } guild && GuildDissolver is { } dissolve)
        {
            error = await dissolve((uint)guild.GuildCode, guild.MasterCharacterId, ct).ConfigureAwait(false);
        }
        var ok = error == GuildRules.Ok;
        _log($"[gm] guild '{guildName}' force dissolve: {(ok ? "done" : $"refused ({error})")}");
        await ctx.SendFrameAsync(ok ? GameMasterFrames.GuildDissolveOk : GameMasterFrames.GuildDissolveFail, GameMasterFrames.GuildName(guildName, _text), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
    }

    public bool IsGameMaster(ulong characterId) => _gameMasters.IsLoggedIn(characterId);

    private bool IsOnline(ulong characterId) => _room.Find(characterId) is not null || _games.ZoneOf(characterId) is not null;

    private Task ToolConnectAsync(S2SessionContext ctx)
    {
        if (_toolsFromLoopbackOnly && (ctx.Remote is not { } remote || !IPAddress.IsLoopback(remote.Address)))
        {
            _log($"[gm] tool connection from {ctx.Remote} refused: not loopback");
            return ctx.Session.DisposeAsync().AsTask();
        }
        _tools[ctx] = null;
        _log($"[gm] tool connection from {ctx.Remote}");
        return Task.CompletedTask;
    }

    private async Task LoginAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        GameMasterOutbox? outbox;
        if (_tools.ContainsKey(ctx))
        {
            if (!GameMasterFrames.TryReadPassword(payload.Span, out _, out var consumed)
                || payload.Span[consumed..].IndexOf((byte)0) < 0)
            {
                _log($"[gm] tool login from {ctx.Remote} dropped: {payload.Length} B");
                return;
            }
            var name = new PacketReader(payload.Span[consumed..]).ReadNulTerminated(_text);
            if (_authority.GmCharacterId(name) is not { } characterId)
            {
                _log($"[gm] tool login from {ctx.Remote} refused: '{name}' is not a GM character");
                await ctx.Session.DisposeAsync().ConfigureAwait(false);
                return;
            }
            outbox = _gameMasters.Login(characterId, name, payload.Span, ClientIp(ctx), tool: true);
            if (outbox is not null && _gameMasters.IsLoggedIn(characterId, tool: true)) _tools[ctx] = new Tool(characterId, name);
        }
        else
        {
            if (_room.Find(ctx) is not { } member) return;
            outbox = _gameMasters.Login(member.Character.CharacterId, member.Character.Name, payload.Span, ClientIp(ctx));
        }
        if (outbox is null) return;
        foreach (var note in outbox.Notes) _log($"[gm] 0x{GameMasterFrames.Login:X4} from {ctx.Remote}: {note}");
        await DeliverAsync(outbox, null, ct, ctx).ConfigureAwait(false);
        if (outbox.Disconnect) await ctx.Session.DisposeAsync().ConfigureAwait(false);
    }

    private sealed record Caller(ulong CharacterId, bool Tool);

    private delegate GameMasterOutbox? GameMasterRequest(Caller caller, ReadOnlySpan<byte> payload);

    private Caller? CallerOf(S2SessionContext ctx)
    {
        if (_tools.TryGetValue(ctx, out var tool)) return tool is null ? null : new Caller(tool.CharacterId, true);
        return _room.Find(ctx) is { } member ? new Caller(member.Character.CharacterId, false) : null;
    }

    private void On(OpcodeDispatcher dispatcher, S2SessionContext ctx, ushort opcode, GameMasterRequest request) =>
        dispatcher.Register(opcode, async (_, payload, ct) =>
        {
            if (CallerOf(ctx) is not { } caller) return;
            if (request(caller, payload.Span) is not { } outbox) return;
            foreach (var note in outbox.Notes) _log($"[gm] 0x{opcode:X4} from 0x{caller.CharacterId:X}: {note}");
            await DeliverAsync(outbox, null, ct, ctx).ConfigureAwait(false);
        });

    private void Command(OpcodeDispatcher dispatcher, S2SessionContext ctx, ushort opcode, GameMasterRequest request) =>
        dispatcher.Register(opcode, async (_, payload, ct) =>
        {
            if (CallerOf(ctx) is not { } caller) return;
            if (!_gameMasters.TryAuthorize(caller.CharacterId, payload.Span, out var consumed, caller.Tool))
            {
                _log($"[gm] 0x{opcode:X4} from 0x{caller.CharacterId:X} refused: no GM login or wrong password");
                var refused = new GameMasterOutbox { Disconnect = true };
                if (!caller.Tool) refused.Kicks.Add(caller.CharacterId);
                await DeliverAsync(refused, null, ct, ctx).ConfigureAwait(false);
                await ctx.Session.DisposeAsync().ConfigureAwait(false);
                return;
            }
            if (request(caller, payload.Span[consumed..]) is not { } outbox)
            {
                _log($"[gm] 0x{opcode:X4} from 0x{caller.CharacterId:X} dropped: {payload.Length} B is not a valid request");
                return;
            }
            foreach (var note in outbox.Notes) _log($"[gm] 0x{opcode:X4} from 0x{caller.CharacterId:X}: {note}");
            Expect(ctx, caller.CharacterId, outbox);
            await DeliverAsync(outbox, null, ct, ctx).ConfigureAwait(false);
        });

    private string[]? ReadStrings(ReadOnlySpan<byte> payload, int count)
    {
        var names = new string[count];
        var rest = payload;
        for (var i = 0; i < count; i++)
        {
            var end = rest.IndexOf((byte)0);
            if (end < 0) return null;
            names[i] = _text.GetString(rest[..end]);
            rest = rest[(end + 1)..];
        }
        return names;
    }

    private (uint ItemId, ushort Count, string Name, PresentDelivery Delivery)? ReadPresent(ReadOnlySpan<byte> payload)
    {
        const int fixedLength = sizeof(uint) + sizeof(ushort);
        if (payload.Length < fixedLength + 1) return null;
        var reader = new PacketReader(payload);
        var itemId = reader.ReadUInt32();
        var count = reader.ReadUInt16();
        var rest = payload[fixedLength..];
        var end = rest.IndexOf((byte)0);
        return end < 0 ? null : (itemId, count, _text.GetString(rest[..end]), GameMasterFrames.ReadPresentDelivery(rest[(end + 1)..]));
    }

    private (string Name, ushort Zone)? ReadNameAndZone(ReadOnlySpan<byte> payload)
    {
        var end = payload.IndexOf((byte)0);
        if (end < 0 || payload.Length < end + 1 + sizeof(ushort)) return null;
        return (_text.GetString(payload[..end]), new PacketReader(payload[(end + 1)..]).ReadUInt16());
    }

    private static string ClientIp(S2SessionContext ctx)
    {
        if (ctx.Remote is not { } remote) return string.Empty;
        var address = remote.Address.IsIPv4MappedToIPv6 ? remote.Address.MapToIPv4() : remote.Address;
        return address.ToString();
    }

    private string? ChatResult(CoupleMessage message, string subject) => message.Opcode switch
    {
        GameMasterFrames.LoginOk => _chat.Result(GameMasterChat.GmFlagSetText, subject),
        GameMasterFrames.TeleportToZoneFail or GameMasterFrames.TeleportToChrFail => _chat.Result(GameMasterChat.NotOnlineText, subject),
        GameMasterFrames.LearnSkillOk => GameMasterChat.LearnSkillDoneLine,
        GameMasterFrames.SetLevelOk => GameMasterChat.SetLevelDoneLine,
        GameMasterFrames.SetLevelFail => GameMasterChat.SetLevelRefusedLine(message.Payload.Length > 0 ? message.Payload[0] : (byte)0),
        GameMasterFrames.LearnSkillFail => GameMasterChat.LearnSkillRefusedLine(message.Payload.Length > 0 ? message.Payload[0] : (byte)0),
        GameMasterFrames.FlyOk => message.Payload.Length > 0 && message.Payload[0] == 1 ? GameMasterChat.FlyOnLine : GameMasterChat.FlyOffLine,
        GameMasterFrames.FlyFail => GameMasterChat.FlyRefusedLine(message.Payload.Length > 0 ? message.Payload[0] : (byte)0),
        GameMasterFrames.PresentItemOk => GameMasterChat.ItemGivenLine,
        GameMasterFrames.PresentItemFail => GameMasterChat.ItemRefusedLine(message.Payload.Length > 0 ? message.Payload[^1] : (byte)0),
        _ => null,
    };

    private async Task DeliverAsync(GameMasterOutbox outbox, ulong? chatOrigin, CancellationToken ct, S2SessionContext? replyTo = null, string subject = "")
    {
        foreach (var message in outbox.Messages)
        {
            if (chatOrigin is { } chatter)
            {
                if (ChatResult(message, subject) is { } text)
                {
                    await _games.SendDirectAsync(chatter, GameMasterFrames.LinkChatReply, GameMasterFrames.ChatLine(chatter, text, _text), ct).ConfigureAwait(false);
                }
                continue;
            }
            var recipient = replyTo ?? _room.Find(message.To)?.Context;
            if (recipient is null)
            {
                _log($"[gm] 0x{message.Opcode:X4} for 0x{message.To:X} skipped: not online");
                continue;
            }
            try
            {
                await recipient.SendFrameAsync(message.Opcode, message.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[gm] 0x{message.Opcode:X4} to 0x{message.To:X} not delivered: {ex.Message}");
            }
        }
        foreach (var direct in outbox.DirectMessages)
        {
            if (_room.Find(direct.To)?.Context is not { } target)
            {
                _log($"[gm] 0x{direct.Opcode:X4} for 0x{direct.To:X} skipped: not online");
                continue;
            }
            try
            {
                await target.SendFrameAsync(direct.Opcode, direct.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[gm] 0x{direct.Opcode:X4} to 0x{direct.To:X} not delivered: {ex.Message}");
            }
        }
        if (chatOrigin is { } texted)
        {
            foreach (var reply in outbox.Texts)
            {
                if (_chat.Result(reply.Name, reply.Subjects.ToArray()) is not { } line) continue;
                await _games.SendDirectAsync(texted, GameMasterFrames.LinkChatReply, GameMasterFrames.ChatLine(texted, line, _text), ct).ConfigureAwait(false);
            }
        }
        foreach (var broadcast in outbox.LobbyBroadcasts)
        {
            foreach (var member in _room.Members.Where(member => member.ChatClientId is not null))
            {
                try
                {
                    await member.Context.SendFrameAsync(broadcast.Opcode, broadcast.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
                {
                    _log($"[gm] 0x{broadcast.Opcode:X4} to 0x{member.Character.CharacterId:X} not delivered: {ex.Message}");
                }
            }
        }
        foreach (var order in outbox.Orders)
        {
            if (await _games.SendAsync(order, ct).ConfigureAwait(false)) continue;
            _log($"[gm] order 0x{order.OrderType:X2} for 0x{order.CharacterId:X} not sent: the character is not on a game server");
            if (GameMasterFrames.ReportOf(order.OrderType) is { } expected) _pending.TryRemove((order.CharacterId, expected), out _);
            if (_gameMasters.NotDelivered(order.CharacterId, order) is { } failure)
            {
                await DeliverAsync(failure, chatOrigin, ct, replyTo, subject).ConfigureAwait(false);
            }
        }
        foreach (var order in outbox.ZoneOrders)
        {
            await _games.SendToZoneAsync(order.Zone, order.OrderType, order.Data, ct).ConfigureAwait(false);
        }
        foreach (var message in outbox.GameServerMessages)
        {
            await _games.BroadcastAsync(message.Opcode, message.Body, ct).ConfigureAwait(false);
        }
        foreach (var kicked in outbox.Kicks)
        {
            await _games.SendDirectAsync(kicked, GameMasterFrames.KickCharacter, GameMasterFrames.Kick(kicked), ct).ConfigureAwait(false);
        }
    }
}
