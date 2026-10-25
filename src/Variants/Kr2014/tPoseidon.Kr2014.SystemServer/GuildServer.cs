using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class GuildServer
{
    private const short NoEtcNum = 0;
    private const int NoGuildValue = 0;

    private readonly GuildService _guilds;
    private readonly SocialRoom _room;
    private readonly GameServerLinks _games;
    private readonly Encoding _text;
    private readonly Action<string> _log;
    private readonly GuildGameRooms _gameRooms;
    private readonly IGuildMatchStore _matches;
    private readonly TimeProvider _time;

    public GuildServer(GuildService guilds, IGuildMatchStore matches, TimeProvider time, SocialRoom room, GameServerLinks games, Encoding text, Action<string> log,
        bool allowSmallGuildGameTeams = false)
    {
        _guilds = guilds ?? throw new ArgumentNullException(nameof(guilds));
        _matches = matches ?? throw new ArgumentNullException(nameof(matches));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _gameRooms = new GuildGameRooms(text, allowSmallGuildGameTeams);
    }

    public GuildGameRooms GameRooms => _gameRooms;

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        On(dispatcher, ctx, GuildFrames.SessionOpen, (id, _) => _guilds.OpenSession(id));
        On(dispatcher, ctx, GuildFrames.OwnInfo, (id, p) =>
            GuildFrames.ReadOwnInfoRequest(p) is { } requested ? _guilds.OwnInfo(id, requested) : null);
        On(dispatcher, ctx, GuildFrames.Create, (id, p) =>
            GuildFrames.ReadCreateRequest(p, _text) is { } create ? _guilds.Create(id, create.GuildName, create.Intro) : null);
        On(dispatcher, ctx, GuildFrames.Apply, (id, p) =>
            GuildFrames.ReadApplyRequest(p) is { } guildCode ? _guilds.Apply(id, guildCode) : null);
        On(dispatcher, ctx, GuildFrames.CancelApply, (id, _) => _guilds.CancelApplication(id));
        On(dispatcher, ctx, GuildFrames.Leave, (id, _) => _guilds.Leave(id));
        On(dispatcher, ctx, GuildFrames.MemberList, (id, p) =>
            GuildFrames.ReadMemberListRequest(p) is { } mode ? _guilds.MemberList(id, mode) : null);
        On(dispatcher, ctx, GuildFrames.VolunteerList, (id, _) => _guilds.VolunteerList(id));
        On(dispatcher, ctx, GuildFrames.AnswerApply, (id, p) =>
            GuildFrames.ReadAnswerApplyRequest(p) is { } answer ? _guilds.AnswerApply(id, answer.ApplicantId, answer.Answer) : null);
        On(dispatcher, ctx, GuildFrames.ForceOut, (id, p) =>
            GuildFrames.ReadForceOutRequest(p) is { } entries ? _guilds.ForceOut(id, entries) : null);
        On(dispatcher, ctx, GuildFrames.Dissolve, (id, _) => _guilds.Dissolve(id));
        On(dispatcher, ctx, GuildFrames.DetailByCode, (id, p) =>
            GuildFrames.ReadDetailByCodeRequest(p) is { } guildCode ? _guilds.DetailByCode(id, guildCode) : null);
        On(dispatcher, ctx, GuildFrames.DetailByCharacter, (id, p) =>
            GuildFrames.ReadDetailByCharacterRequest(p, _text) is { } name ? _guilds.DetailByCharacter(id, name) : null);
        dispatcher.Register(GuildFrames.GuildSms, (_, payload, ct) => GuildSmsAsync(ctx, payload, ct));
        dispatcher.Register(GuildGameRooms.DetailRequest, async (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { } member || !GuildGameRooms.TryReadDetail(payload.Span, out var gameType, out var roomId)) return;
            var (opcode, body) = _gameRooms.DetailOf(gameType, roomId);
            _log($"[guild] room detail type {gameType} room {roomId} for 0x{member.Character.CharacterId:X}: 0x{opcode:X4}");
            await member.Context.SendFrameAsync(opcode, body, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        });
        dispatcher.Register(GuildGameRooms.ListRequest, (_, _, ct) =>
            _room.Find(ctx) is { } member ? SendAsync(member, GuildGameRooms.List, _gameRooms.RoomList(RoomCaptions, LadderRank), ct) : Task.CompletedTask);
        dispatcher.Register(GuildGameRooms.Make, (_, payload, ct) => MakeRoomAsync(ctx, payload, ct));
        dispatcher.Register(GuildGameRooms.Join, (_, payload, ct) => JoinRoomAsync(ctx, payload, ct));
        dispatcher.Register(GuildGameRooms.InviteRequest, (_, payload, ct) => InviteAsync(ctx, payload, ct));
        dispatcher.Register(GuildGameRooms.InviteAnswer, (_, payload, ct) => AnswerAsync(ctx, payload, ct));
        dispatcher.Register(GuildGameRooms.RemoveRequest, (_, payload, ct) => RemoveAsync(ctx, payload, ct));
        dispatcher.Register(GuildGameRooms.LeaveRequest, (_, _, ct) =>
            _room.Find(ctx) is { } member ? LeaveRoomAsync(member.Character.CharacterId, ct) : Task.CompletedTask);
        dispatcher.Register(GuildGameRooms.StartRequest, (_, _, ct) => StartRoomAsync(ctx, ct));
        dispatcher.Register(GuildGameRooms.LadderListRequest, (_, _, ct) =>
            _room.Find(ctx) is { } member ? SendAsync(member, GuildGameRooms.LadderList, _gameRooms.LadderListFor(LadderRows()), ct) : Task.CompletedTask);
        dispatcher.Register(GuildGameRooms.PlayoffRequest, (_, _, ct) =>
            _room.Find(ctx) is { } member ? SendAsync(member, GuildGameRooms.PlayoffList, GuildGameRooms.Code(GuildGameRooms.NoPlayoff), ct) : Task.CompletedTask);
        dispatcher.Register(GuildGameOrders.StartNoticeRequest, (_, payload, ct) => StartNoticeAsync(ctx, payload, ct));
    }

    public GuildRoomCaptions RoomCaptions { get; set; } = GuildRoomCaptions.None;

    private async Task MakeRoomAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        if (GuildGameRooms.ReadMake(payload.Span, _text) is not { } request)
        {
            _log($"[guild] game room make from 0x{member.Character.CharacterId:X} not read ({payload.Length} B)");
            return;
        }
        if (request.GameType == GuildGameOrders.MonsterMatchType)
        {
            _log($"[guild] game room make type {request.GameType} from 0x{member.Character.CharacterId:X}: the monster match room path is not built");
            return;
        }
        var result = _gameRooms.TryMake(Entrant(member), request, InLobby(member));
        if (result.Error is not null || result.Room is not { } room)
        {
            _log($"[guild] game room make from 0x{member.Character.CharacterId:X} refused: 0x{result.ErrorOpcode:X4} code {result.Error}");
            await SendAsync(member, result.ErrorOpcode, _gameRooms.Refusal(result.Error ?? 0), ct).ConfigureAwait(false);
            return;
        }
        var side = room.Sides[0];
        await SendAsync(member, GuildGameRooms.MakeOk, _gameRooms.MadeFor(room, Invitable(side), LadderRank), ct).ConfigureAwait(false);
        await _games.SendToZoneAsync(GuildGameOrders.LobbyZone, GuildGameOrders.OrderLobbyNotice,
            GuildGameOrders.LobbyNotice(room.GameType, room.MapChoice, side.GuildName, side.Members.Count, side.AverageLevel, _text), ct).ConfigureAwait(false);
        _log($"[guild] game room {room.Key} type {room.GameType} mode {request.Mode} made by '{member.Character.Name}' (up to {room.MaxParticipants} per side, map choice {room.MapChoice}, ladder flag {room.NoLadderFlag})");
    }

    private async Task JoinRoomAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        if (GuildGameRooms.ReadJoin(payload.Span, _text) is not { } request)
        {
            _log($"[guild] game room join from 0x{member.Character.CharacterId:X} not read ({payload.Length} B)");
            return;
        }
        var result = _gameRooms.TryJoin(Entrant(member), request, InLobby(member));
        if (result.Error is not null || result.Room is not { } room)
        {
            _log($"[guild] game room join {request.RoomKey} from 0x{member.Character.CharacterId:X} refused: 0x{result.ErrorOpcode:X4} code {result.Error}");
            await SendAsync(member, result.ErrorOpcode, _gameRooms.Refusal(result.Error ?? 0), ct).ConfigureAwait(false);
            return;
        }
        var side = room.Sides.Count - 1;
        await SendToAsync(room.Everyone, GuildGameRooms.JoinOk, _gameRooms.JoinedFor(room, side), ct).ConfigureAwait(false);
        await SendAsync(member, GuildGameRooms.RoomState, _gameRooms.StateFor(room, LadderRank), ct).ConfigureAwait(false);
        _log($"[guild] game room {room.Key} joined by '{member.Character.Name}' of guild {room.Sides[side].GuildCode}");
    }

    private async Task InviteAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member || Entrant(member) is not { } inviter) return;
        if (GuildGameRooms.ReadName(payload.Span, _text) is not { } name)
        {
            _log($"[guild] game room invite from 0x{member.Character.CharacterId:X} not read ({payload.Length} B)");
            return;
        }
        var invited = _room.FindNamed(name) is { } named && InLobby(named) ? Entrant(named) : null;
        var result = _gameRooms.Invite(inviter, invited);
        if (result.Error is not null || invited is null)
        {
            var code = result.Error ?? GuildGameRooms.MemberCheckFailed;
            _log($"[guild] game room invite of '{name}' by '{member.Character.Name}' refused: code {code}");
            await SendAsync(member, GuildGameRooms.InviteRefusedNotice, _gameRooms.NameAndCode(name, code), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(member, GuildGameRooms.InvitePending, _gameRooms.Name(invited.Name), ct).ConfigureAwait(false);
        if (_room.Find(invited.CharacterId) is { } invitee) await SendAsync(invitee, GuildGameRooms.Invitation, Array.Empty<byte>(), ct).ConfigureAwait(false);
        _log($"[guild] game room {result.Room?.Key}: '{member.Character.Name}' invites '{invited.Name}'");
    }

    private async Task AnswerAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        if (!GuildGameRooms.TryReadAnswer(payload.Span, out var accepted, out var reason))
        {
            _log($"[guild] game room answer from 0x{member.Character.CharacterId:X} not read ({payload.Length} B)");
            return;
        }
        if (_gameRooms.Answer(member.Character.CharacterId, accepted) is not { } answer)
        {
            _log($"[guild] game room answer from 0x{member.Character.CharacterId:X}: no invitation");
            return;
        }
        if (!answer.Joined)
        {
            var code = accepted ? GuildGameRooms.TooManyParticipants : reason;
            if (_room.Find(answer.Side.Master.CharacterId) is { } master)
            {
                await SendAsync(master, GuildGameRooms.InviteRefusedNotice, _gameRooms.NameAndCode(answer.Invitee.Name, code), ct).ConfigureAwait(false);
            }
            _log($"[guild] game room {answer.Room.Key}: '{answer.Invitee.Name}' does not join (code {code})");
            return;
        }
        await SendToAsync(answer.Room.Everyone.Where(entrant => entrant.CharacterId != answer.Invitee.CharacterId), GuildGameRooms.ParticipantAdded,
            _gameRooms.Participant(answer.Invitee), ct).ConfigureAwait(false);
        await SendAsync(member, GuildGameRooms.RoomState, _gameRooms.StateFor(answer.Room, LadderRank), ct).ConfigureAwait(false);
        _log($"[guild] game room {answer.Room.Key}: '{answer.Invitee.Name}' joins guild {answer.Side.GuildCode} ({answer.Side.Members.Count} of {answer.Room.MaxParticipants})");
    }

    private async Task RemoveAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        if (GuildGameRooms.ReadName(payload.Span, _text) is not { } name
            || _gameRooms.Remove(member.Character.CharacterId, name) is not { } removal)
        {
            _log($"[guild] game room removal from 0x{member.Character.CharacterId:X} dropped ({payload.Length} B)");
            return;
        }
        var removed = _gameRooms.Removed(removal.Removed.Name, removal.By.Name);
        await SendToAsync(removal.Room.Everyone.Append(removal.Removed), GuildGameRooms.RemovedNotice, removed, ct).ConfigureAwait(false);
        _log($"[guild] game room {removal.Room.Key}: '{removal.By.Name}' removes '{removal.Removed.Name}'");
    }

    private async Task LeaveRoomAsync(ulong characterId, CancellationToken ct)
    {
        if (_gameRooms.Leave(characterId) is not { } left) return;
        switch (left.Kind)
        {
            case GuildRoomLeaveKind.MemberLeft:
                await SendToAsync(left.Notified, GuildGameRooms.RemovedNotice, _gameRooms.Removed(left.Leaver.Name, left.Leaver.Name), ct).ConfigureAwait(false);
                break;
            case GuildRoomLeaveKind.SideLeft:
                await SendToAsync(left.Notified, GuildGameRooms.SideLeftNotice, GuildGameRooms.Code(left.Side.GuildCode), ct).ConfigureAwait(false);
                break;
            case GuildRoomLeaveKind.RoomClosed:
                await SendToAsync(left.Notified, GuildGameRooms.RoomClosedNotice, GuildGameRooms.Code(GuildGameRooms.RoomClosedCode), ct).ConfigureAwait(false);
                break;
        }
        _log($"[guild] game room {left.Room.Key}: '{left.Leaver.Name}' leaves ({left.Kind})");
    }

    private async Task StartRoomAsync(S2SessionContext ctx, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        var result = _gameRooms.TryStart(member.Character.CharacterId);
        if (result.Error is not null || result.Room is not { } room)
        {
            _log($"[guild] game room start from 0x{member.Character.CharacterId:X} refused: code {result.Error}");
            await SendAsync(member, result.ErrorOpcode, _gameRooms.Refusal(result.Error ?? 0), ct).ConfigureAwait(false);
            return;
        }
        foreach (var side in room.Sides) await EnterMatchAsync(room, side, ct).ConfigureAwait(false);
        _log($"[guild] game room {room.Key} starts: {string.Join(" vs ", room.Sides.Select(side => $"guild {side.GuildCode} ({side.Members.Count})"))}");
    }

    private List<GuildGameEntrant> Invitable(GuildRoomSide side) =>
        _room.Members
            .Where(online => online.Character.CharacterId != side.Master.CharacterId && InLobby(online))
            .Select(Entrant)
            .OfType<GuildGameEntrant>()
            .Where(entrant => entrant.GuildCode == side.GuildCode && _gameRooms.RoomOf(entrant.CharacterId) is null)
            .ToList();

    private List<GuildLadderListRow> LadderRows() =>
        _matches.TopLadder(GuildGameRooms.LadderListLength)
            .Select(row => new GuildLadderListRow(row.GuildName, row.Standing.GuildCode, row.Standing.Matches, row.Standing.Wins, row.Standing.Losses, row.Standing.Ladder))
            .ToList();

    private async Task SendToAsync(IEnumerable<GuildGameEntrant> recipients, ushort opcode, byte[] body, CancellationToken ct)
    {
        foreach (var characterId in recipients.Select(entrant => entrant.CharacterId).Distinct().ToArray())
        {
            if (_room.Find(characterId) is { } online) await SendAsync(online, opcode, body, ct).ConfigureAwait(false);
        }
    }

    private async Task StartNoticeAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member) return;
        if (!GuildGameOrders.TryReadStartNoticeRequest(payload.Span, out var gameType))
        {
            _log($"[guild] game start notice from 0x{member.Character.CharacterId:X} not read ({payload.Length} B)");
            return;
        }
        if (Entrant(member) is not { } entrant || _gameRooms.Find(entrant.GuildCode) is not { } room)
        {
            _log($"[guild] game start notice from 0x{member.Character.CharacterId:X}: no room of the own guild");
            return;
        }
        var sides = room.Sides.Select(side => new GuildNoticeSide(side.GuildName, side.Members.Count, LadderRank(side.GuildCode), side.AverageLevel)).ToList();
        while (sides.Count < GuildGameRooms.SidesPerRoom) sides.Add(GuildNoticeSide.Empty);
        await _games.SendToZoneAsync(GuildGameOrders.LobbyZone, GuildGameOrders.OrderStartNotice,
            GuildGameOrders.StartNotice(gameType, room.MapChoice, sides[0], sides[1], _text), ct).ConfigureAwait(false);
        _log($"[guild] game room {room.Key} start notice (type {gameType}) to the lobby");
    }

    public async Task MatchResultAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (GuildGameOrders.ReadResult(payload.Span, _text) is not { } result)
        {
            _log($"[guild] guild game result of {payload.Length} B not read");
            return;
        }
        _gameRooms.Close(result.RoomKey);
        _log($"[guild] game room {result.RoomKey} finished: win team {result.WinTeam}, result {result.MatchResult}");
        if (result.Sides.Count < GuildGameOrders.ResultSides) return;
        if (result.MatchResult == GuildLadderRules.ResultNone)
        {
            await SendLadderChangeAsync(result, null, ct).ConfigureAwait(false);
            return;
        }
        var settled = StoreMatch(result);
        await SendLadderChangeAsync(result, settled, ct).ConfigureAwait(false);
        await _games.SendToZoneAsync(GuildGameOrders.LobbyZone, GuildGameOrders.OrderResultNotice, GuildGameOrders.ResultNotice(result, _text), ct).ConfigureAwait(false);
    }

    private byte LadderRank(uint guildCode) => GuildLadderRules.DisplayedRank(_matches.LadderPosition(guildCode));

    private Task SendLadderChangeAsync(GuildMatchResult result, GuildLadderSettlement? settled, CancellationToken ct) =>
        _games.SendToZoneAsync(result.MapId, GuildGameOrders.OrderLadderChange, GuildGameOrders.LadderChange(result.RoomKey,
            ResultSide(result.Sides[0].GuildCode, settled?.Make), ResultSide(result.Sides[1].GuildCode, settled?.Challenge)), ct);

    private GuildResultSide ResultSide(uint guildCode, GuildLadderSideChange? change)
    {
        var standing = _matches.Standing(guildCode);
        return new GuildResultSide(standing?.Matches ?? 0, standing?.Wins ?? 0, standing?.Losses ?? 0, standing?.Draws ?? 0,
            change?.GetLadder ?? 0, change?.TotalLadder ?? GuildLadderRules.Rating(standing));
    }

    private GuildLadderSettlement StoreMatch(GuildMatchResult result)
    {
        var make = result.Sides[0];
        var challenge = result.Sides[1];
        var makeStanding = _matches.Standing(make.GuildCode);
        var challengeStanding = _matches.Standing(challenge.GuildCode);
        var winner = result.WinTeam == 0 ? make : challenge;
        var loser = result.WinTeam == 0 ? challenge : make;
        var today = _time.GetLocalNow();
        var dayStart = new DateTimeOffset(today.Date, today.Offset);
        var over = _matches.MatchesBetween(winner.GuildCode, loser.GuildCode, dayStart, dayStart.AddDays(1)) >= GuildLadderRules.DailyMatchLimit;
        var settled = GuildLadderRules.Settle(result.MatchResult, result.WinTeam, result.NoLadderChange, over,
            new GuildLadderEntrant(makeStanding, make.Count), new GuildLadderEntrant(challengeStanding, challenge.Count));
        var record = new GuildMatchRecord(
            ToMinute(result.Begin), ToMinute(result.End), result.GameType, result.MapId, result.Terms.TimeLimitMinutes,
            result.Terms.WinningCount(result.GameType), NoEtcNum,
            SideRecord(make, settled.Make), SideRecord(challenge, settled.Challenge));
        var error = _matches.Insert(record);
        _log($"[guild] game room {result.RoomKey} stored (error {error}): guild {make.GuildCode} {settled.Make.Outcome} ladder {settled.Make.GetLadder:+0;-0;0} -> {settled.Make.TotalLadder}, "
            + $"guild {challenge.GuildCode} {settled.Challenge.Outcome} ladder {settled.Challenge.GetLadder:+0;-0;0} -> {settled.Challenge.TotalLadder}"
            + (over ? $", daily limit of {GuildLadderRules.DailyMatchLimit} matches reached" : string.Empty));
        return settled;
    }

    private static GuildMatchSideRecord SideRecord(GuildMatchSideResult side, GuildLadderSideChange change) => new(
        side.GuildCode, side.Count, (byte)Math.Min(side.Kills, byte.MaxValue), (byte)Math.Min(side.Deaths, byte.MaxValue),
        change.GetLadder, change.TotalLadder, NoGuildValue, change.Outcome);

    private static DateTimeOffset ToMinute(DateTimeOffset time) =>
        new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, time.Offset);

    public Task RoomInvalidAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (GuildGameOrders.ReadRoomInvalid(payload.Span) is { } invalid && _gameRooms.Close(invalid.RoomKey) is not null)
        {
            _log($"[guild] game room {invalid.RoomKey} type {invalid.GameType} closed: no players left");
        }
        return Task.CompletedTask;
    }

    private Task EnterMatchAsync(GuildRoomEntry room, GuildRoomSide side, CancellationToken ct) =>
        _games.SendOnceAsync(side.Master.CharacterId, GuildGameOrders.OrderEnterMatch, GuildGameOrders.EnterMatch(
            new GuildEnterMatch(room.Key, side.GuildCode, room.MapChoice, room.NoLadderFlag, string.Empty, room.GameType, side.GuildName,
                side.Members.Select(entrant => entrant.CharacterId).ToArray(), _matches.Standing(side.GuildCode)?.League ?? GuildLeagueRecord.None), _text), ct);

    private GuildGameEntrant? Entrant(SocialMember member) =>
        _guilds.GameEntrant(member.Character.CharacterId, (ushort)Math.Clamp((int)member.Character.Level, 0, ushort.MaxValue));

    private bool InLobby(SocialMember member) => _games.ZoneOf(member.Character.CharacterId) == GuildGameOrders.LobbyZone;

    private async Task SendAsync(SocialMember member, ushort opcode, byte[] body, CancellationToken ct)
    {
        try
        {
            await member.Context.SendFrameAsync(opcode, body, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[guild] 0x{opcode:X4} to 0x{member.Character.CharacterId:X} not delivered: {ex.Message}");
        }
    }

    public Task LoggedInAsync(SocialMember member, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(member);
        return DeliverAsync(_guilds.Login(member.Character.CharacterId, member.Character.Name), ct);
    }

    public async Task ClosedAsync(SocialMember member, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(member);
        await LeaveRoomAsync(member.Character.CharacterId, ct).ConfigureAwait(false);
        await DeliverAsync(_guilds.Logout(member.Character.CharacterId), ct).ConfigureAwait(false);
    }

    public Task ZoneEnteredAsync(ulong characterId, CancellationToken ct) => DeliverAsync(_guilds.ZoneEntered(characterId), ct);

    public void Renamed(ulong characterId, string newName) => _guilds.MemberRenamed(characterId, newName);

    private delegate GuildOutbox? GuildRequest(ulong characterId, ReadOnlySpan<byte> payload);

    private void On(OpcodeDispatcher dispatcher, S2SessionContext ctx, ushort opcode, GuildRequest request) =>
        dispatcher.Register(opcode, (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { } member) return Task.CompletedTask;
            _log($"[guild] 0x{opcode:X4} from 0x{member.Character.CharacterId:X} '{member.Character.Name}' [{Convert.ToHexString(payload.Span)}]");
            if (request(member.Character.CharacterId, payload.Span) is not { } outbox)
            {
                _log($"[guild] 0x{opcode:X4} from 0x{member.Character.CharacterId:X} dropped: {payload.Length} B is shorter than the request");
                return Task.CompletedTask;
            }
            return DeliverAsync(outbox, ct);
        });

    public IFriendStore? Friends { get; set; }

    private async Task GuildSmsAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { } member || GuildFrames.ReadGuildSms(payload.Span, _text) is not { } sms) return;
        var owner = member.Character.CharacterId;
        var plan = _guilds.GuildSms(owner, sms.GuildCode);
        switch (plan.Outcome)
        {
            case GuildSmsOutcome.OwnGuild:
                await ctx.SendFrameAsync(GuildFrames.GuildSmsOwnGuild, Array.Empty<byte>(), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
                break;
            case GuildSmsOutcome.Deliver when _room.Find(plan.MasterCharacterId) is { } master:
                await master.Context.SendFrameAsync(FriendFrames.GuildSmsReceived,
                    FriendFrames.GuildSmsFrom(owner, plan.SenderName, sms.Text, _text), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
                _log($"[guild] sms from 0x{owner:X} to the master 0x{plan.MasterCharacterId:X} of guild {sms.GuildCode}");
                break;
            case GuildSmsOutcome.Deliver:
                var stored = Friends?.SendMessage(plan.MasterCharacterId, owner, FriendRules.GuildMessageKind, FriendRules.Truncate(sms.Text, FriendRules.MessageContentLength, _text)) == true;
                _log($"[guild] sms from 0x{owner:X} to the offline master 0x{plan.MasterCharacterId:X} of guild {sms.GuildCode}: {(stored ? "stored" : "not stored")}");
                break;
        }
    }

    public async Task<int> ForceDissolveAsync(uint guildCode, ulong masterCharacterId, CancellationToken ct)
    {
        var (error, outbox) = _guilds.ForceDissolve(guildCode, masterCharacterId);
        await DeliverAsync(outbox, ct).ConfigureAwait(false);
        return error;
    }

    private async Task DeliverAsync(GuildOutbox messages, CancellationToken ct)
    {
        foreach (var message in messages)
        {
            if (_room.Find(message.To) is not { } recipient)
            {
                _log($"[guild] 0x{message.Opcode:X4} for 0x{message.To:X} skipped: not online");
                continue;
            }
            try
            {
                await recipient.Context.SendFrameAsync(message.Opcode, message.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
                _log($"[guild] 0x{message.Opcode:X4} -> 0x{message.To:X} '{recipient.Character.Name}' ({message.Payload.Length} B)");
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[guild] 0x{message.Opcode:X4} to 0x{message.To:X} not delivered: {ex.Message}");
            }
        }
        foreach (var order in messages.Orders)
        {
            await _games.SendOnceAsync(order.CharacterId, SystemLinkFrames.OrderGuildName, GuildFrames.NameOrderFor(order.GuildName, order.Alias, _text), ct)
                .ConfigureAwait(false);
        }
    }
}
