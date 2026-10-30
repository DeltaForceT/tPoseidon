using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guild;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;
using tPoseidon.Kr2014.Wire;

internal sealed class GuildGameController : IDisposable
{
    private const byte NoGoal = 0;

    private readonly SystemServerLink _link;
    private readonly Func<GuildGameTypeTables> _types;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly Func<InGameSessionState, (ushort X, ushort Y)?, Task> _revive;
    private readonly MonsterManager _monsters;
    private readonly IReadOnlyDictionary<ushort, ZoneGeometry> _geometry;
    private readonly Func<MonsterMatchTables> _monsterTables;
    private readonly Func<MonsterEntity, byte[]> _spawnFrame;
    private readonly Func<ushort, byte[]> _despawnFrame;
    private readonly Func<InGameSessionState, IReadOnlyList<InGameSessionState>, Task> _hideFromFighters;
    private readonly Func<GuildGameRewardTables> _rewards;
    private readonly GuildGameRewardGranter _granter;
    private readonly object _killGate = new();
    private readonly List<(MonsterEntity Monster, ushort Killer)> _monsterKills = new();
    private readonly Dictionary<uint, HashSet<ulong>> _watchers = new();
    private readonly HashSet<ulong> _observers = new();
    private readonly object _observerGate = new();
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Dictionary<uint, GuildMatch> _matches = new();
    private readonly Dictionary<uint, DateTimeOffset> _battleBegan = new();
    private readonly Dictionary<uint, MatchEnd> _awaitingLadder = new();
    private readonly SemaphoreSlim _sync = new(1, 1);
    private readonly Random _rng = new();
    private readonly Dictionary<uint, DateTimeOffset> _firstSummonAt = new();
    private readonly Dictionary<uint, DateTimeOffset> _boardAt = new();
    private readonly Dictionary<(uint Room, ulong Character), (int Region, int Milliseconds)> _dwell = new();
    private DateTimeOffset _lastTick;

    private const int SummonDwellMilliseconds = 2500;
    private const int FirstSummonDelayMilliseconds = 2000;
    private const int BoardPeriodMilliseconds = 1000;
    private const int FirstTrigger = 0;
    private const byte SummonFromRegion = 0;
    private const byte SummonFromTimer = 1;
    private const byte NoPvpMode = 0;

    public GuildGameController(
        SystemServerLink link, Func<GuildGameTypeTables> types, Func<IReadOnlyCollection<InGameSessionState>> sessions,
        Func<ushort, Opcode, byte[], Task> sendToMap, Func<InGameSessionState, (ushort X, ushort Y)?, Task> revive,
        MonsterManager monsters, IReadOnlyDictionary<ushort, ZoneGeometry> geometry, Func<MonsterMatchTables> monsterTables,
        Func<MonsterEntity, byte[]> spawnFrame, Func<ushort, byte[]> despawnFrame, Func<InGameSessionState, IReadOnlyList<InGameSessionState>, Task> hideFromFighters,
        Func<GuildGameRewardTables> rewards, GuildGameRewardGranter granter, Encoding text, string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _types = types ?? throw new ArgumentNullException(nameof(types));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _revive = revive ?? throw new ArgumentNullException(nameof(revive));
        _monsters = monsters ?? throw new ArgumentNullException(nameof(monsters));
        _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        _monsterTables = monsterTables ?? throw new ArgumentNullException(nameof(monsterTables));
        _spawnFrame = spawnFrame ?? throw new ArgumentNullException(nameof(spawnFrame));
        _despawnFrame = despawnFrame ?? throw new ArgumentNullException(nameof(despawnFrame));
        _hideFromFighters = hideFromFighters ?? throw new ArgumentNullException(nameof(hideFromFighters));
        _rewards = rewards ?? throw new ArgumentNullException(nameof(rewards));
        _granter = granter ?? throw new ArgumentNullException(nameof(granter));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
        _monsters.MonsterKilled += OnMonsterKilled;
    }

    public void Dispose()
    {
        _link.OrderReceived -= OrderAsync;
        _monsters.MonsterKilled -= OnMonsterKilled;
        _sync.Dispose();
    }

    private Task OrderAsync(OfficerOrder order, CancellationToken ct) => order.OrderType switch
    {
        GuildGameOrders.OrderEnterMatch => Locked(() => EnterMatchAsync(order)),
        GuildGameOrders.OrderWatchMatch => Locked(() => WatchMatchAsync(order)),
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
            var monsterMatch = enter.GameType == GuildGameRules.MonsterMatchType ? new MonsterMatch(_monsterTables(), enter.ScoreGoal) : null;
            match = new GuildMatch(enter.RoomKey, enter.GameType, (ushort)map, type.TimeLimit, enter.NoLadderFlag, monsterMatch);
            _matches[enter.RoomKey] = match;
        }
        var team = match.AddSide(enter.GuildCode, enter.GuildName, order.CharacterId, enter.League, enter.Ladder);
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

    private async Task WatchMatchAsync(OfficerOrder order)
    {
        if (GuildGameOrders.ReadWatchMatch(order.Data) is not { } roomKey)
        {
            Console.WriteLine($"{_logTag} [guild game] ORDER 0xEA for 0x{order.CharacterId:X} not read ({order.Data.Length} B)");
            return;
        }
        if (Session(order.CharacterId) is not { } watcher || watcher.MapId != GuildGameOrders.LobbyZone)
        {
            Console.WriteLine($"{_logTag} [guild game] ORDER 0xEA room {roomKey}: 0x{order.CharacterId:X} is not in the lobby {GuildGameOrders.LobbyZone}");
            return;
        }
        if (!_matches.TryGetValue(roomKey, out var match) || match.State >= GuildMatchState.Close)
        {
            Console.WriteLine($"{_logTag} [guild game] ORDER 0xEA room {roomKey}: no match is running for 0x{order.CharacterId:X}");
            return;
        }
        if (MatchOf(order.CharacterId) is not null) return;
        if (!_watchers.TryGetValue(roomKey, out var watching)) _watchers[roomKey] = watching = new HashSet<ulong>();
        watching.Add(order.CharacterId);
        SetObserver(order.CharacterId, true);
        await WarpAsync(watcher, match.MapId, WatcherArrival(match.MapId)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [guild game] room {roomKey}: 0x{order.CharacterId:X} watches on map {match.MapId}");
    }

    private (ushort X, ushort Y)? WatcherArrival(ushort mapId)
    {
        if (!_geometry.TryGetValue(mapId, out var geometry) || geometry.MaxX <= geometry.MinX || geometry.MaxY <= geometry.MinY) return null;
        return ((ushort)Math.Clamp((geometry.MaxX - geometry.MinX) / 2 + geometry.MinX, 0, ushort.MaxValue),
            (ushort)Math.Clamp((geometry.MaxY - geometry.MinY) / 2 + geometry.MinY, 0, ushort.MaxValue));
    }

    private uint? WatchedRoom(ulong characterId)
    {
        foreach (var (room, watching) in _watchers)
        {
            if (watching.Contains(characterId)) return room;
        }
        return null;
    }

    public bool IsObserver(InGameSessionState st)
    {
        ArgumentNullException.ThrowIfNull(st);
        if (st.Character is not { } character) return false;
        lock (_observerGate) return _observers.Contains(character.CharacterId);
    }

    public uint EntityFlagsOf(ulong characterId)
    {
        lock (_observerGate) return _observers.Contains(characterId) ? PlayerSpawnLoginPacket.ObserverEntityFlags : PlayerSpawnLoginPacket.DefaultEntityFlags;
    }

    public bool HidesFrom(InGameSessionState subject, InGameSessionState viewer) =>
        GuildGameVisibility.Hides(IsObserver(subject), IsObserver(viewer));

    private void SetObserver(ulong characterId, bool observing)
    {
        lock (_observerGate)
        {
            if (observing) _observers.Add(characterId);
            else _observers.Remove(characterId);
        }
    }

    private void LeaveMatch(GuildMatch match, ulong characterId)
    {
        SetObserver(characterId, false);
        match.Leave(characterId);
    }

    private void StopWatching(ulong characterId)
    {
        SetObserver(characterId, false);
        foreach (var room in _watchers.Keys.ToArray())
        {
            _watchers[room].Remove(characterId);
            if (_watchers[room].Count == 0) _watchers.Remove(room);
        }
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
        if (st.Character is { } watcher && WatchedRoom(watcher.CharacterId) is not null)
        {
            StopWatching(watcher.CharacterId);
            await WarpAsync(st, GuildGameOrders.LobbyZone).ConfigureAwait(false);
            return;
        }
        if (st.Character is not { } character || MatchOf(character.CharacterId) is not { } match) return;
        SetObserver(character.CharacterId, false);
        match.BadQuit(character.CharacterId);
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: 0x{st.PlayerEntityId:X4} quit (state {match.State})");
        await WarpAsync(st, GuildGameOrders.LobbyZone).ConfigureAwait(false);
    });

    public Task DenyAsync(InGameSessionState st) => Locked(async () =>
    {
        if (st.Character is { } watcher && WatchedRoom(watcher.CharacterId) is not null)
        {
            StopWatching(watcher.CharacterId);
            await WarpAsync(st, GuildGameOrders.LobbyZone).ConfigureAwait(false);
            return;
        }
        if (st.Character is not { } character || MatchOf(character.CharacterId) is not { } match) return;
        LeaveMatch(match, character.CharacterId);
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
            if (match.GameType == GuildGameRules.DeathMatch) await DefeatedToObserverAsync(victim, dead.CharacterId).ConfigureAwait(false);
        }
        else
        {
            await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_KILL, GuildGameFrames.MonsterMatchPlayerKill(kill.VictimId, kill.KillerId, kill.RespawnFlag)).ConfigureAwait(false);
            await SendMonsterBoardAsync(match).ConfigureAwait(false);
        }
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: kills {match.TeamKills[0]}/{match.TeamKills[1]}, deaths {match.TeamDeaths[0]}/{match.TeamDeaths[1]}");
    });

    private async Task DefeatedToObserverAsync(InGameSessionState defeated, ulong characterId)
    {
        SetObserver(characterId, true);
        defeated.PvpMode = NoPvpMode;
        await _revive(defeated, null).ConfigureAwait(false);
        var fighters = _sessions()
            .Where(other => !ReferenceEquals(other, defeated) && other.MapId == defeated.MapId && other.Character is not null && !IsObserver(other))
            .ToArray();
        await _hideFromFighters(defeated, fighters).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [guild game] 0x{defeated.PlayerEntityId:X4} is defeated and watches the rest of the match");
    }

    public Task EnteredZoneAsync(InGameSessionState st) => Locked(() =>
    {
        if (st.Character is not { } character) return Task.CompletedTask;
        if (MatchOf(character.CharacterId) is { } match && st.MapId != match.MapId) LeaveMatch(match, character.CharacterId);
        if (WatchedRoom(character.CharacterId) is { } room && (!_matches.TryGetValue(room, out var watched) || st.MapId != watched.MapId)) StopWatching(character.CharacterId);
        return Task.CompletedTask;
    });

    public Task LeftAsync(InGameSessionState st) => Locked(() =>
    {
        if (st.Character is not { } character) return Task.CompletedTask;
        StopWatching(character.CharacterId);
        if (MatchOf(character.CharacterId) is { } match) LeaveMatch(match, character.CharacterId);
        return Task.CompletedTask;
    });

    public Task TickAsync(DateTimeOffset now) => Locked(async () =>
    {
        var elapsed = _lastTick == default ? 0 : (int)Math.Clamp((now - _lastTick).TotalMilliseconds, 0, int.MaxValue);
        _lastTick = now;
        var kills = DrainMonsterKills();
        foreach (var match in _matches.Values.ToArray())
        {
            foreach (var raised in match.Tick(elapsed)) await ApplyAsync(match, raised, now).ConfigureAwait(false);
            if (match.Monsters is not null && match.State == GuildMatchState.Battle) await TickMonstersAsync(match, elapsed, now, kills).ConfigureAwait(false);
        }
    });

    private List<(MonsterEntity Monster, ushort Killer)> DrainMonsterKills()
    {
        lock (_killGate)
        {
            var drained = _monsterKills.ToList();
            _monsterKills.Clear();
            return drained;
        }
    }

    private void OnMonsterKilled(MonsterEntity monster, ushort killer)
    {
        lock (_killGate) _monsterKills.Add((monster, killer));
    }

    private async Task TickMonstersAsync(GuildMatch match, int elapsed, DateTimeOffset now, List<(MonsterEntity Monster, ushort Killer)> kills)
    {
        var monsters = match.Monsters!;
        if (_firstSummonAt.TryGetValue(match.RoomKey, out var due) && now >= due)
        {
            _firstSummonAt.Remove(match.RoomKey);
            if (monsters.Touch(FirstTrigger) is { } first) await SummonAsync(match, first, SummonFromTimer).ConfigureAwait(false);
        }
        foreach (var (monster, killer) in kills)
        {
            if (monsters.MonsterKilled(monster.EntityId, KillerCharacter(killer)) is not { } kill) continue;
            await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_KILL,
                GuildGameFrames.MonsterKill(kill.SameTeam, (ushort)kill.MonsterId, kill.ObjectId, kill.KillerId)).ConfigureAwait(false);
            await SendMonsterBoardAsync(match).ConfigureAwait(false);
        }
        if (!_geometry.TryGetValue(match.MapId, out var geometry)) return;
        foreach (var fighter in match.Fighters.ToArray())
        {
            var key = (match.RoomKey, fighter.CharacterId);
            if (Session(fighter.CharacterId) is not { IsDead: false } session
                || geometry.RegionContaining(ZoneGeometry.RegionTypeMonsterAppearance, session.PlayerX, session.PlayerY) is not { } region)
            {
                _dwell.Remove(key);
                continue;
            }
            var spent = _dwell.TryGetValue(key, out var known) && known.Region == region.Id ? known.Milliseconds + elapsed : elapsed;
            _dwell[key] = (region.Id, spent);
            if (spent > SummonDwellMilliseconds && monsters.Touch(region.Id) is { } touched) await SummonAsync(match, touched, SummonFromRegion).ConfigureAwait(false);
        }
        if (!_boardAt.TryGetValue(match.RoomKey, out var boardDue) || now >= boardDue)
        {
            _boardAt[match.RoomKey] = now.AddMilliseconds(BoardPeriodMilliseconds);
            await SendMonsterBoardAsync(match).ConfigureAwait(false);
        }
    }

    private ulong KillerCharacter(ushort killerEntity) =>
        _sessions().FirstOrDefault(session => session.PlayerEntityId == killerEntity)?.Character?.CharacterId ?? 0;

    private async Task SummonAsync(GuildMatch match, MatchSummon summon, byte flag)
    {
        _geometry.TryGetValue(match.MapId, out var geometry);
        for (var team = 0; team < GuildGameRules.Teams; team++)
        {
            var home = geometry?.Points.FirstOrDefault(point => point.Type == ZoneGeometry.PointTypeRegionSpawn && point.Id == team + 1);
            if (home is not { } spot)
            {
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: map {match.MapId} has no monster spawn point {team + 1}");
                continue;
            }
            var monster = _monsters.Spawn(summon.MonsterId, match.MapId,
                (ushort)Math.Clamp(spot.X, 0, ushort.MaxValue), (ushort)Math.Clamp(spot.Y, 0, ushort.MaxValue), null, null, team + 1);
            if (monster is null) continue;
            monster.PvpMode = GuildMatch.PvpModeOf(team);
            summon.Monsters[team] = monster.EntityId;
            await _sendToMap(match.MapId, Opcode.NPC_SPAWN, _spawnFrame(monster)).ConfigureAwait(false);
        }
        await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_MONSTER_SUMMON,
            GuildGameFrames.MonsterSummon((byte)summon.TriggerId, flag, summon.Monsters[0] ?? 0, summon.Monsters[1] ?? 0)).ConfigureAwait(false);
        await SendMonsterBoardAsync(match).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: trigger {summon.TriggerId} summons monster {summon.MonsterId} for both teams");
    }

    private async Task RemoveSummonedAsync(GuildMatch match, IEnumerable<ushort> entities)
    {
        foreach (var entity in entities)
        {
            if (_monsters.Remove(entity)) await _sendToMap(match.MapId, Opcode.S2C_ENTITY_DESPAWN, _despawnFrame(entity)).ConfigureAwait(false);
        }
    }

    private async Task SendMonsterBoardAsync(GuildMatch match)
    {
        if (match.Monsters is not { } monsters) return;
        var sides = match.Sides.Select(side => new GuildScoreSide(side.GuildCode, side.GuildName)).ToArray();
        var entries = new List<GuildMonsterScoreEntry>();
        foreach (var fighter in match.Fighters)
        {
            if (Session(fighter.CharacterId) is not { Character: { } character } session) continue;
            entries.Add(new GuildMonsterScoreEntry(
                (byte)fighter.Team, (ushort)session.PlayerCharacterType, (ushort)(session.Stats?.Level ?? character.Level), character.Name,
                fighter.CharacterId, 0, Clamp16(fighter.Score), Clamp16(fighter.Kills), Clamp16(fighter.Deaths), Clamp16(fighter.WrongKills)));
        }
        var tallies = new IReadOnlyList<BossTally>[] { monsters.Tally(0), monsters.Tally(1) };
        var live = new IReadOnlyList<GuildSummonLine>[GuildGameRules.Teams];
        for (var team = 0; team < GuildGameRules.Teams; team++)
        {
            live[team] = monsters.Summons
                .Where(summon => summon.Monsters[team] is not null && !summon.Dead[team])
                .Select(summon => new GuildSummonLine(summon.Monsters[team]!.Value, summon.MonsterId, (ushort)Math.Clamp(summon.SecondsLeft, 0, ushort.MaxValue)))
                .ToArray();
        }
        var payload = GuildGameFrames.MonsterScoreBoard(match.RoomKey, sides, (uint)Math.Max(match.SecondsLeft, 0), entries, tallies, live, _text);
        await SendMatchAsync(match, Opcode.S2C_GUILD_GAME_SCORE, payload).ConfigureAwait(false);
    }

    private static ushort Clamp16(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);

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
                if (match.GameType <= GuildGameRules.DeathMatch)
                {
                    await SendScoreBoardAsync(match).ConfigureAwait(false);
                }
                else
                {
                    _firstSummonAt[match.RoomKey] = now.AddMilliseconds(FirstSummonDelayMilliseconds);
                    await SendMonsterBoardAsync(match).ConfigureAwait(false);
                }
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: battle on map {match.MapId}, {modes.Count} fighters, {match.SecondsLeft} s");
                break;
            case GuildMatchRespawn respawn:
                foreach (var characterId in respawn.CharacterIds)
                {
                    if (Session(characterId) is { IsDead: true } fallen) await _revive(fallen, RespawnPoint(match, characterId)).ConfigureAwait(false);
                }
                break;
            case GuildMatchSummonsExpired expired:
                await RemoveSummonedAsync(match, expired.Expired.Select(line => line.Monster)).ConfigureAwait(false);
                break;
            case GuildMatchEnded ended:
                var end = ended.End;
                await ClearMonsterMatchAsync(match).ConfigureAwait(false);
                if (end.Result == GuildGameRules.ResultDecided) await RewardBestFighterAsync(match, end).ConfigureAwait(false);
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
                    LeaveMatch(match, fighter.CharacterId);
                }
                foreach (var watcherId in _watchers.GetValueOrDefault(match.RoomKey)?.ToArray() ?? Array.Empty<ulong>())
                {
                    if (Session(watcherId) is { } watching) await WarpAsync(watching, GuildGameOrders.LobbyZone).ConfigureAwait(false);
                }
                Forget(match);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey} closed");
                break;
            case GuildMatchInvalid:
                await ClearMonsterMatchAsync(match).ConfigureAwait(false);
                await _link.SendAsync(GuildGameOrders.RoomInvalid, GuildGameOrders.RoomInvalidFor(match.GameType, match.RoomKey), default).ConfigureAwait(false);
                Forget(match);
                Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: no fighters left, room invalid");
                break;
        }
    }

    private async Task RewardBestFighterAsync(GuildMatch match, MatchEnd end)
    {
        var winners = match.Fighters
            .Where(fighter => fighter.Team == GuildMatch.WinnerTeam(end) && Session(fighter.CharacterId) is not null)
            .Select(fighter => StandingOf(fighter, Session(fighter.CharacterId)!))
            .ToArray();
        if (GuildGameBestFighter.Choose(match.GameType, winners) is not { } bestId || Session(bestId) is not { } best) return;
        var average = match.AverageLadder;
        if (_rewards().RowFor(average) is not { } row || row.Items.Count == 0 || row.Items.Any(item => item.ItemId == 0))
        {
            Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: best fighter 0x{bestId:X}, average ladder {average}: no reward row");
            return;
        }
        var (operations, given) = _granter.Grant(bestId, row.Items);
        if (given.Count > 0) await SendAsync(best, Opcode.S2C_GUILD_GAME_REWARD, QuestResultFrames.ItemsTaken(operations, given)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [guild game] room {match.RoomKey}: best fighter 0x{bestId:X}, average ladder {average}: reward row {row.Score} gives {given.Count} of {row.Items.Count} items");
    }

    private static FighterStanding StandingOf(MatchFighter fighter, InGameSessionState session) => new(
        fighter.CharacterId, fighter.Kills, fighter.Deaths, fighter.Score, fighter.WrongKills,
        session.Stats?.Level ?? session.Character?.Level ?? 0, (int)Math.Min(session.Stats?.Exp ?? 0UL, int.MaxValue));

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

    private (ushort X, ushort Y)? RespawnPoint(GuildMatch match, ulong characterId)
    {
        if (match.FighterOf(characterId) is not { } fighter || !_geometry.TryGetValue(match.MapId, out var geometry)) return null;
        return GuildArenaSpawn.Pick(geometry, fighter.Team, bound => { lock (_rng) return _rng.Next(bound); });
    }

    private async Task ClearMonsterMatchAsync(GuildMatch match)
    {
        if (match.Monsters is not { } monsters) return;
        await RemoveSummonedAsync(match, monsters.Summons.SelectMany(summon => summon.Monsters).OfType<ushort>().ToArray()).ConfigureAwait(false);
    }

    private void Forget(GuildMatch match)
    {
        foreach (var fighter in match.Fighters) SetObserver(fighter.CharacterId, false);
        foreach (var watcherId in _watchers.GetValueOrDefault(match.RoomKey) ?? new HashSet<ulong>()) SetObserver(watcherId, false);
        _watchers.Remove(match.RoomKey);
        _firstSummonAt.Remove(match.RoomKey);
        _boardAt.Remove(match.RoomKey);
        foreach (var key in _dwell.Keys.Where(key => key.Room == match.RoomKey).ToArray()) _dwell.Remove(key);
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
        foreach (var watcherId in _watchers.GetValueOrDefault(match.RoomKey)?.ToArray() ?? Array.Empty<ulong>())
        {
            if (Session(watcherId) is { } watching) await SendAsync(watching, opcode, payload).ConfigureAwait(false);
        }
    }

    private static Task WarpAsync(InGameSessionState st, ushort zone, (ushort X, ushort Y)? position = null)
    {
        if (st.MapId == zone || st.RequestZoneChange is not { } requestZoneChange) return Task.CompletedTask;
        return requestZoneChange(new ZoneChangeRequest(zone, TeleportArrival.ChangeZoneType, null, TimeSpan.Zero, position), default);
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
