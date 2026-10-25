using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guild;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

internal sealed class GuildGameController : IDisposable
{
    private const byte NoGoal = 0;

    private readonly SystemServerLink _link;
    private readonly Func<GuildGameTypeTables> _types;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Func<InGameSessionState, Task> _revive;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Dictionary<uint, GuildMatch> _matches = new();
    private readonly Dictionary<uint, DateTimeOffset> _battleBegan = new();
    private readonly Dictionary<uint, MatchEnd> _awaitingLadder = new();
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Random _rng = new();
    private DateTimeOffset _lastTick;

    public GuildGameController(
        SystemServerLink link, Func<GuildGameTypeTables> types, Func<IReadOnlyCollection<InGameSessionState>> sessions,
        Func<ushort, Opcode, byte[], Task> sendToMap, Func<InGameSessionState, Task> revive, Encoding text, string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _types = types ?? throw new ArgumentNullException(nameof(types));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _revive = revive ?? throw new ArgumentNullException(nameof(revive));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
    }

    public void Dispose()
    {
        _link.OrderReceived -= OrderAsync;
        _sync.Dispose();
    }

    private Task OrderAsync(OfficerOrder order, CancellationToken ct) => order.OrderType switch
    {
        GuildGameOrders.OrderEnterMatch => Locked(() => EnterMatchAsync(order)),
        GuildGameOrders.OrderLobbyNotice => _sendToMap(GuildGameOrders.LobbyZone, Opcode.S2C_GUILD_GAME_ROOM_NOTICE, order.Data),
        GuildGameOrders.OrderStartNotice => _sendToMap(GuildGameOrders.LobbyZone, Opcode.S2C_GUILD_GAME_START_NOTICE, order.Data),
        GuildGameOrders.OrderResultNotice => _sendToMap(GuildGameOrders.LobbyZone, Opcode.S2C_GUILD_GAME_RESULT_NOTICE, order.Data),
        GuildGameOrders.OrderLadderChange => Locked(() => LadderChangeAsync(order)),
        _ => Task.CompletedTask,
    };

    private async Task EnterMatchAsync(OfficerOrder order)
    {
        if (GuildGameOrders.ReadEnterMatch(order.Data, _text) is not { } enter)
        {
            Console.WriteLine($"{_logTag} [guild game] ORDER 0xE9 for 0x{order.CharacterId:X} not read ({order.Data.Length} B)");
            return;
        }
        if (Session(order.CharacterId) is not { } requester || requester.MapId != GuildGameOrders.LobbyZone)
        {
            Console.WriteLine($"{_logTag} [guild game] ORDER 0xE9 room {enter.RoomKey}: 0x{order.CharacterId:X} is not in the lobby {GuildGameOrders.LobbyZone}");
            return;
        }
        if (!_types().Types.TryGetValue(enter.GameType, out var type) || type.MapOf(enter.MapChoice) is not { } map)
        {
            Console.WriteLine($"{_logTag} [guild game] ORDER 0xE9 room {enter.RoomKey}: no arena for type {enter.GameType} map choice {enter.MapChoice}");
            return;
        }
        if (!_matches.TryGetValue(enter.RoomKey, out var match))
        {
            match = new GuildMatch(enter.RoomKey, enter.GameType, (ushort)map, type.TimeLimit, enter.NoLadderFlag);
            _matches[enter.RoomKey] = match;
        }
        var team = match.AddSide(enter.GuildCode, enter.GuildName, order.CharacterId, enter.League);
        if (team < 0)
        {
            Console.WriteLine($"{_logTag} [guild game] ORDER 0xE9 room {enter.RoomKey}: guild {enter.GuildCode} cannot take a side (state {match.State})");
            return;
        }
        var entered = 0;
        foreach (var memberId in enter.Members)
        {
            if (Session(memberId) is not { } member || member.MapId != GuildGameOrders.LobbyZone || match.Enter(memberId, team) is null) continue;
            entered++;
            await WarpAsync(member, match.MapId).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} [guild game] room {enter.RoomKey}: guild {enter.GuildCode} '{enter.GuildName}' is team {team} on map {match.MapId} ({entered}/{enter.Members.Count} members moved)");
    }

    public Task ReadyAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || MatchOf(character.CharacterId) is not { } match) return;
        switch (match.ToggleReady(character.CharacterId, st.GameMasterAttribute))
        {
            case GuildMatchReadyChanged ready:
                await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_READY, GuildGameFrames.Ready(ready.SideA, ready.SideB)).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: ready {ready.SideA}/{ready.SideB}");
                break;
            case GuildMatchGmReadyChanged gm:
                await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_GM_READY, GuildGameFrames.GmReady(gm.Ready)).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: GM ready {gm.Ready}");
                break;
        }
    });

    public Task BadQuitAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || MatchOf(character.CharacterId) is not { } match) return;
        match.BadQuit(character.CharacterId);
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: 0x{st.PlayerEntityId:X4} quit (state {match.State})");
        await WarpAsync(st, GuildGameOrders.LobbyZone).ConfigureAwait(false);
    });

    public Task DenyAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is not { } character || MatchOf(character.CharacterId) is not { } match) return;
        match.Leave(character.CharacterId);
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: 0x{st.PlayerEntityId:X4} left the room");
        await WarpAsync(st, GuildGameOrders.LobbyZone).ConfigureAwait(false);
    });

    public Task PlayerKilledAsync(InGameSessionState victim, InGameSessionState killer) => Locked(async () =>
    {
        if (victim.Character is not { } dead || killer.Character is not { } winner || MatchOf(dead.CharacterId) is not { } match) return;
        if (match.PlayerKilled(dead.CharacterId, winner.CharacterId) is not { } kill) return;
        if (match.GameType <= GuildGameRules.DeathMatch)
        {
            await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_KILL, GuildGameFrames.Kill(match.GameType, kill.KillerId, kill.VictimId, kill.RespawnFlag)).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: kills {match.TeamKills[0]}/{match.TeamKills[1]}, deaths {match.TeamDeaths[0]}/{match.TeamDeaths[1]}");
    });

    public Task EnteredZoneAsync(InGameSessionState st) => Locked(() =>
    {
        if (st.Character is { } character && MatchOf(character.CharacterId) is { } match && st.MapId != match.MapId) match.Leave(character.CharacterId);
        return Task.CompletedTask;
    });

    public Task LeftAsync(InGameSessionState st) => Locked(() =>
    {
        if (st.Character is { } character && MatchOf(character.CharacterId) is { } match) match.Leave(character.CharacterId);
        return Task.CompletedTask;
    });

    public Task TickAsync(DateTimeOffset now) => Locked(async () =>
    {
        var elapsed = _lastTick == default ? 0 : (int)Math.Clamp((now - _lastTick).TotalMilliseconds, 0, int.MaxValue);
        _lastTick = now;
        foreach (var match in _matches.Values.ToArray())
        {
            foreach (var raised in match.Tick(elapsed)) await ApplyAsync(match, raised, now).ConfigureAwait(false);
        }
    });

    private async Task ApplyAsync(GuildMatch match, GuildMatchEvent raised, DateTimeOffset now)
    {
        switch (raised)
        {
            case GuildMatchCountdown countdown:
                await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_COUNTDOWN, GuildGameFrames.Countdown(countdown.SecondsLeft)).ConfigureAwait(false);
                break;
            case GuildMatchBattleBegan:
                _battleBegan[match.RoomKey] = now;
                var modes = new List<GuildGameFighterMode>();
                foreach (var fighter in match.Fighters)
                {
                    if (Session(fighter.CharacterId) is not { } session) continue;
                    session.PvpMode = GuildMatch.PvpModeOf(fighter.Team);
                    modes.Add(new GuildGameFighterMode(session.PlayerEntityId, session.PvpMode));
                }
                await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_START, GuildGameFrames.BattleStart(modes)).ConfigureAwait(false);
                if (match.GameType <= GuildGameRules.DeathMatch) await SendScoreBoardAsync(match).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: battle on map {match.MapId}, {modes.Count} fighters, {match.SecondsLeft} s");
                break;
            case GuildMatchRespawn respawn:
                foreach (var characterId in respawn.CharacterIds)
                {
                    if (Session(characterId) is { IsDead: true } fallen) await _revive(fallen).ConfigureAwait(false);
                }
                break;
            case GuildMatchEnded ended:
                var end = ended.End;
                if (_link.IsConnected)
                {
                    _awaitingLadder[match.RoomKey] = end;
                }
                else
                {
                    await SendResultAsync(match, end, GuildResultSide.Before(LeagueOf(match, 0)), GuildResultSide.Before(LeagueOf(match, 1))).ConfigureAwait(false);
                }
                var began = _battleBegan.GetValueOrDefault(match.RoomKey, now);
                await _link.SendAsync(GuildGameOrders.MatchResult, GuildGameOrders.Result(ResultOf(match, end, began, now), _text), default).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: winner {end.Winner}, result {end.Result}, reason {end.Reason}");
                break;
            case GuildMatchClosing:
                if (_awaitingLadder.Remove(match.RoomKey, out var unsettled))
                {
                    await SendResultAsync(match, unsettled, GuildResultSide.Before(LeagueOf(match, 0)), GuildResultSide.Before(LeagueOf(match, 1))).ConfigureAwait(false);
                    Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: no ladder change from the system server, result sent with the record before the match");
                }
                foreach (var fighter in match.Fighters.ToArray())
                {
                    if (Session(fighter.CharacterId) is { } session) await WarpAsync(session, GuildGameOrders.LobbyZone).ConfigureAwait(false);
                    match.Leave(fighter.CharacterId);
                }
                Forget(match);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey} closed");
                break;
            case GuildMatchInvalid:
                await _link.SendAsync(GuildGameOrders.RoomInvalid, GuildGameOrders.RoomInvalidFor(match.GameType, match.RoomKey), default).ConfigureAwait(false);
                Forget(match);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: no fighters left, room invalid");
                break;
        }
    }

    private async Task LadderChangeAsync(OfficerOrder order)
    {
        if (GuildGameOrders.ReadLadderChange(order.Data) is not { } change)
        {
            Console.WriteLine($"{_logTag} [guild game] ladder change of {order.Data.Length} B not read");
            return;
        }
        if (!_matches.TryGetValue(change.RoomKey, out var match) || !_awaitingLadder.Remove(change.RoomKey, out var end)) return;
        await SendResultAsync(match, end, change.Team0, change.Team1).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: ladder {change.Team0.GetLadder:+0;-0;0} -> {change.Team0.TotalLadder} / {change.Team1.GetLadder:+0;-0;0} -> {change.Team1.TotalLadder}");
    }

    private async Task SendScoreBoardAsync(GuildMatch match)
    {
        var sides = match.Sides.Select(side => new GuildScoreSide(side.GuildCode, side.GuildName)).ToArray();
        var entries = new List<GuildScoreEntry>();
        foreach (var fighter in match.Fighters)
        {
            if (Session(fighter.CharacterId) is not { Character: { } character } session) continue;
            entries.Add(new GuildScoreEntry(
                (byte)fighter.Team, (ushort)session.PlayerCharacterType, (ushort)(session.Stats?.Level ?? character.Level), character.Name,
                fighter.CharacterId, 0, (ushort)Math.Clamp(fighter.Kills, 0, ushort.MaxValue), (ushort)Math.Clamp(fighter.Deaths, 0, ushort.MaxValue),
                match.IsDown(fighter.CharacterId)));
        }
        var payload = GuildGameFrames.ScoreBoard(match.RoomKey, match.GameType, sides, (uint)Math.Max(match.SecondsLeft, 0), entries, _text);
        await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_SCORE, payload).ConfigureAwait(false);
    }

    private Task SendResultAsync(GuildMatch match, MatchEnd end, GuildResultSide team0, GuildResultSide team1) =>
        SendMatchAsync(match, Opcode.S2C_GUILD_GAME_RESULT, GuildGameFrames.Result((byte)GuildMatch.WinnerTeam(end), end.Result, end.Reason, team0, team1));

    private void Forget(GuildMatch match)
    {
        _awaitingLadder.Remove(match.RoomKey);
        _matches.Remove(match.RoomKey);
        _battleBegan.Remove(match.RoomKey);
    }

    private static GuildLeagueRecord LeagueOf(GuildMatch match, int team) =>
        team < match.Sides.Count ? match.Sides[team].League : GuildLeagueRecord.None;

    private GuildMatchResult ResultOf(GuildMatch match, MatchEnd end, DateTimeOffset began, DateTimeOffset ended)
    {
        var sides = new List<GuildMatchSideResult>(GuildGameOrders.ResultSides);
        for (var team = 0; team < match.Sides.Count; team++)
        {
            var side = match.Sides[team];
            sides.Add(new GuildMatchSideResult(
                side.GuildCode,
                (byte)Math.Min(match.Fighters.Count(fighter => fighter.Team == team), byte.MaxValue),
                (ushort)Math.Clamp(match.TeamKills[team], 0, ushort.MaxValue),
                (ushort)Math.Clamp(match.TeamDeaths[team], 0, ushort.MaxValue),
                side.GuildName));
        }
        var timeLimit = _types().Types.TryGetValue(match.GameType, out var type) ? (byte)Math.Clamp(type.TimeLimit, 0, byte.MaxValue) : (byte)0;
        return new GuildMatchResult(match.RoomKey, NoLadderChange: match.NoLadderFlag != 0, match.GameType, new GuildMatchTerms(timeLimit, NoGoal, NoGoal), match.MapId,
            began, ended, (byte)GuildMatch.WinnerTeam(end), end.Result, sides);
    }

    private GuildMatch? MatchOf(ulong characterId) => _matches.Values.FirstOrDefault(match => match.FighterOf(characterId) is not null);

    private InGameSessionState? Session(ulong characterId) =>
        _sessions().FirstOrDefault(session => session.Character?.CharacterId == characterId);

    private async Task SendMatchAsync(GuildMatch match, Opcode opcode, byte[] payload)
    {
        foreach (var fighter in match.Fighters.ToArray())
        {
            if (Session(fighter.CharacterId) is { } session) await SendAsync(session, opcode, payload).ConfigureAwait(false);
        }
    }

    private static Task WarpAsync(InGameSessionState st, ushort zone)
    {
        if (st.MapId == zone || st.RequestZoneChange is not { } requestZoneChange) return Task.CompletedTask;
        return requestZoneChange(new ZoneChangeRequest(zone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, null), default);
    }

    private async Task Locked(Func<Task> action)
    {
        await _sync.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _sync.Release();
        }
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rng) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
