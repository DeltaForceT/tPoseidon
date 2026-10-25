using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Guild;

public enum GuildMatchState
{
    Room = 0,
    Countdown = 1,
    Battle = 2,
    Result = 3,
    Close = 4,
    Invalid = 5,
}

public sealed class GuildMatchSide
{
    public GuildMatchSide(uint guildCode, string guildName, ulong masterId, GuildLeagueRecord league)
    {
        GuildCode = guildCode;
        GuildName = guildName;
        MasterId = masterId;
        League = league;
    }

    public uint GuildCode { get; }
    public string GuildName { get; }
    public ulong MasterId { get; }
    public GuildLeagueRecord League { get; }
    public bool Ready { get; set; }
    public int BadQuits { get; set; }
}

public abstract record GuildMatchEvent;

public sealed record GuildMatchReadyChanged(bool SideA, bool SideB) : GuildMatchEvent;

public sealed record GuildMatchGmReadyChanged(bool Ready) : GuildMatchEvent;

public sealed record GuildMatchCountdown(ushort SecondsLeft) : GuildMatchEvent;

public sealed record GuildMatchBattleBegan : GuildMatchEvent;

public sealed record GuildMatchEnded(MatchEnd End) : GuildMatchEvent;

public sealed record GuildMatchClosing : GuildMatchEvent;

public sealed record GuildMatchInvalid : GuildMatchEvent;

public sealed record GuildMatchRespawn(IReadOnlyList<ulong> CharacterIds) : GuildMatchEvent;

public sealed record GuildMatchKill(ulong KillerId, ulong VictimId, byte RespawnFlag);

public sealed class GuildMatch
{
    public const int ReadyHoldMs = 500;
    public const ushort CountdownSeconds = 5;
    public const int ResultSeconds = 120;
    public const int SecondMs = 1000;
    public const int SecondsPerMinute = 60;
    public const byte TeamPvpModeBase = 3;
    public const float RespawnHpRatio = 0.30f;
    public const int RespawnWaveSeconds = 30;
    public const int RespawnWaveGraceSeconds = 2;
    public const int RespawnMinimumSeconds = 3;

    private readonly List<GuildMatchSide> _sides = new();
    private readonly Dictionary<ulong, MatchFighter> _fighters = new();
    private readonly HashSet<ulong> _down = new();
    private readonly List<(ulong CharacterId, int DueSecondsLeft)> _respawns = new();
    private int _readyHeldMs;
    private int _secondCarryMs;

    public const byte EventRoomLadderFlag = 2;

    public GuildMatch(uint roomKey, byte gameType, ushort mapId, int timeLimitMinutes, byte noLadderFlag)
    {
        RoomKey = roomKey;
        GameType = gameType;
        MapId = mapId;
        SecondsLeft = timeLimitMinutes * SecondsPerMinute;
        NoLadderFlag = noLadderFlag;
        GmReady = noLadderFlag != EventRoomLadderFlag;
    }

    public uint RoomKey { get; }
    public byte GameType { get; }
    public ushort MapId { get; }
    public byte NoLadderFlag { get; }
    public GuildMatchState State { get; private set; } = GuildMatchState.Room;
    public bool GmReady { get; private set; }
    public ushort CountdownLeft { get; private set; }
    public int SecondsLeft { get; private set; }
    public int ResultLeft { get; private set; }
    public MatchEnd? End { get; private set; }
    public int[] TeamKills { get; } = new int[GuildGameRules.Teams];
    public int[] TeamDeaths { get; } = new int[GuildGameRules.Teams];
    public IReadOnlyList<GuildMatchSide> Sides => _sides;
    public IReadOnlyCollection<MatchFighter> Fighters => _fighters.Values;

    public static byte PvpModeOf(int team) => (byte)(TeamPvpModeBase + (team != 0 ? 1 : 0));

    public int AddSide(uint guildCode, string guildName, ulong masterId, GuildLeagueRecord league)
    {
        ArgumentNullException.ThrowIfNull(guildName);
        if (_sides.FindIndex(side => side.GuildCode == guildCode) is var known and >= 0) return known;
        if (_sides.Count >= GuildGameRules.Teams || State != GuildMatchState.Room) return -1;
        _sides.Add(new GuildMatchSide(guildCode, guildName, masterId, league));
        return _sides.Count - 1;
    }

    public MatchFighter? Enter(ulong characterId, int team)
    {
        if (team < 0 || team >= _sides.Count || State != GuildMatchState.Room) return null;
        if (_fighters.TryGetValue(characterId, out var known)) return known;
        var fighter = new MatchFighter(characterId, team);
        _fighters[characterId] = fighter;
        return fighter;
    }

    public MatchFighter? FighterOf(ulong characterId) => _fighters.GetValueOrDefault(characterId);

    public bool IsDown(ulong characterId) => _down.Contains(characterId);

    public GuildMatchEvent? ToggleReady(ulong characterId, bool isGm)
    {
        if (State != GuildMatchState.Room) return null;
        var side = _sides.FindIndex(candidate => candidate.MasterId == characterId);
        if (side >= 0)
        {
            _sides[side].Ready = !_sides[side].Ready;
            _readyHeldMs = 0;
            return new GuildMatchReadyChanged(_sides.Count > 0 && _sides[0].Ready, _sides.Count > 1 && _sides[1].Ready);
        }
        if (!isGm) return null;
        GmReady = !GmReady;
        _readyHeldMs = 0;
        return new GuildMatchGmReadyChanged(GmReady);
    }

    public bool BadQuit(ulong characterId)
    {
        if (!_fighters.Remove(characterId, out var fighter)) return false;
        if (State == GuildMatchState.Battle) _sides[fighter.Team].BadQuits++;
        _down.Remove(characterId);
        _respawns.RemoveAll(pending => pending.CharacterId == characterId);
        return true;
    }

    public bool Leave(ulong characterId)
    {
        _down.Remove(characterId);
        _respawns.RemoveAll(pending => pending.CharacterId == characterId);
        return _fighters.Remove(characterId);
    }

    public GuildMatchKill? PlayerKilled(ulong victimId, ulong killerId)
    {
        if (State != GuildMatchState.Battle || !_fighters.TryGetValue(victimId, out var victim)) return null;
        victim.Deaths++;
        TeamDeaths[victim.Team]++;
        if (killerId != victimId && _fighters.TryGetValue(killerId, out var killer))
        {
            killer.Kills++;
            TeamKills[killer.Team]++;
        }
        if (GameType == GuildGameRules.DeathMatch)
        {
            _down.Add(victimId);
            return new GuildMatchKill(killerId, victimId, 0);
        }
        var phase = SecondsLeft % RespawnWaveSeconds;
        var nearWave = phase <= RespawnWaveGraceSeconds;
        var delay = nearWave ? phase + RespawnMinimumSeconds : phase;
        _respawns.RemoveAll(pending => pending.CharacterId == victimId);
        _respawns.Add((victimId, SecondsLeft - delay));
        return new GuildMatchKill(killerId, victimId, nearWave ? (byte)1 : (byte)0);
    }

    public IReadOnlyList<GuildMatchEvent> Tick(int elapsedMs)
    {
        var events = new List<GuildMatchEvent>();
        switch (State)
        {
            case GuildMatchState.Room:
                if (_sides.Count == GuildGameRules.Teams && _sides.All(side => side.Ready) && GmReady)
                {
                    _readyHeldMs += elapsedMs;
                    if (_readyHeldMs >= ReadyHoldMs)
                    {
                        State = GuildMatchState.Countdown;
                        CountdownLeft = CountdownSeconds;
                        _secondCarryMs = 0;
                    }
                }
                else
                {
                    _readyHeldMs = 0;
                }
                break;
            case GuildMatchState.Countdown:
                foreach (var _ in Seconds(elapsedMs))
                {
                    events.Add(new GuildMatchCountdown(CountdownLeft));
                    if (CountdownLeft == 0)
                    {
                        State = GuildMatchState.Battle;
                        events.Add(new GuildMatchBattleBegan());
                        break;
                    }
                    CountdownLeft--;
                }
                break;
            case GuildMatchState.Battle:
                if (_fighters.Count == 0)
                {
                    State = GuildMatchState.Invalid;
                    events.Add(new GuildMatchInvalid());
                    break;
                }
                if (InstantEnd() is { } instant)
                {
                    Finish(instant, events);
                    break;
                }
                foreach (var _ in Seconds(elapsedMs))
                {
                    SecondsLeft--;
                    if (SecondsLeft > 0)
                    {
                        if (DueRespawns() is { Length: > 0 } due) events.Add(new GuildMatchRespawn(due));
                        continue;
                    }
                    Finish(TimeUp(), events);
                    break;
                }
                break;
            case GuildMatchState.Result:
                if (_fighters.Count == 0)
                {
                    State = GuildMatchState.Close;
                    events.Add(new GuildMatchClosing());
                    break;
                }
                foreach (var _ in Seconds(elapsedMs))
                {
                    ResultLeft--;
                    if (ResultLeft > 0) continue;
                    State = GuildMatchState.Close;
                    events.Add(new GuildMatchClosing());
                    break;
                }
                break;
        }
        return events;
    }

    private IEnumerable<int> Seconds(int elapsedMs)
    {
        _secondCarryMs += elapsedMs;
        while (_secondCarryMs >= SecondMs)
        {
            _secondCarryMs -= SecondMs;
            yield return SecondMs;
        }
    }

    private void Finish(MatchEnd end, List<GuildMatchEvent> events)
    {
        End = end;
        State = GuildMatchState.Result;
        ResultLeft = ResultSeconds;
        _secondCarryMs = 0;
        events.Add(new GuildMatchEnded(end));
    }

    private ulong[] DueRespawns()
    {
        var due = _respawns.Where(pending => pending.DueSecondsLeft >= SecondsLeft).Select(pending => pending.CharacterId).ToArray();
        _respawns.RemoveAll(pending => pending.DueSecondsLeft >= SecondsLeft);
        return due;
    }

    private int Standing(int team) => _fighters.Values.Count(fighter => fighter.Team == team && !_down.Contains(fighter.CharacterId));

    public MatchEnd? InstantEnd()
    {
        if (GameType != GuildGameRules.DeathMatch) return null;
        for (var team = 0; team < GuildGameRules.Teams; team++)
        {
            if (Standing(team) == 0 && Standing(1 - team) > 0)
            {
                return new MatchEnd(GuildGameRules.WinnerCode(1 - team), GuildGameRules.ResultDecided, GuildGameRules.ReasonNoFighters);
            }
        }
        return null;
    }

    public MatchEnd TimeUp()
    {
        var values = GameType == GuildGameRules.DeathMatch
            ? new[] { Standing(0), Standing(1) }
            : TeamKills;
        if (values[0] != values[1])
        {
            return new MatchEnd(GuildGameRules.WinnerCode(values[0] > values[1] ? 0 : 1), GuildGameRules.ResultDecided, GuildGameRules.ReasonTimeUp);
        }
        var anyDeath = TeamDeaths.Any(deaths => deaths != 0);
        return new MatchEnd(GuildGameRules.WinnerDraw, anyDeath ? GuildGameRules.ResultDraw : GuildGameRules.ResultNoScore, GuildGameRules.ReasonTimeUp);
    }

    public static int WinnerTeam(MatchEnd end) => end.Winner == GuildGameRules.WinnerTeam1 ? 1 : 0;
}
