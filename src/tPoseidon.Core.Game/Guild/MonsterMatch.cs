using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Guild;

public static class GuildGameRules
{
    public const byte TeamMatch = 0;
    public const byte DeathMatch = 1;
    public const int Teams = 2;
    public const byte WinnerDraw = 1;
    public const byte WinnerTeam0 = 2;
    public const byte WinnerTeam1 = 3;
    public const byte ResultNoScore = 0;
    public const byte ResultDraw = 2;
    public const byte ResultDecided = 3;
    public const byte ReasonTimeUp = 1;
    public const byte ReasonScoreGoal = 2;
    public const byte ReasonNoFighters = 3;
    public const int SummonLifetimeSeconds = 300;

    public static byte WinnerCode(int team) => team == 0 ? WinnerTeam0 : WinnerTeam1;
}

public sealed class MatchFighter
{
    public MatchFighter(ulong characterId, int team)
    {
        CharacterId = characterId;
        Team = team;
    }

    public ulong CharacterId { get; }
    public int Team { get; }
    public int Score { get; set; }
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int WrongKills { get; set; }
}

public sealed class MatchSummon
{
    public MatchSummon(int triggerId, int monsterId)
    {
        TriggerId = triggerId;
        MonsterId = monsterId;
    }

    public int TriggerId { get; }
    public int MonsterId { get; }
    public int SecondsLeft { get; set; } = GuildGameRules.SummonLifetimeSeconds;
    public ushort?[] Monsters { get; } = new ushort?[GuildGameRules.Teams];
    public bool[] Dead { get; } = new bool[GuildGameRules.Teams];
    public bool Cleared => Dead.All(dead => dead) || SecondsLeft <= 0;
}

public readonly record struct MatchEnd(byte Winner, byte Result, byte Reason);

public sealed class MonsterMatch
{
    private readonly MonsterMatchTables _points;
    private readonly List<MatchFighter> _fighters = new();
    private readonly Dictionary<int, MatchSummon> _summons = new();
    private int _tickCarryMs;

    public MonsterMatch(MonsterMatchTables points, int scoreGoal)
    {
        _points = points ?? throw new ArgumentNullException(nameof(points));
        ScoreGoal = scoreGoal;
    }

    public int ScoreGoal { get; }
    public int[] TeamScore { get; } = new int[GuildGameRules.Teams];
    public int[] TeamKills { get; } = new int[GuildGameRules.Teams];
    public int[] TeamDeaths { get; } = new int[GuildGameRules.Teams];
    public IReadOnlyList<MatchFighter> Fighters => _fighters;
    public IReadOnlyCollection<MatchSummon> Summons => _summons.Values;

    public MatchFighter Join(ulong characterId, int team)
    {
        if (team is < 0 or >= GuildGameRules.Teams) throw new ArgumentOutOfRangeException(nameof(team));
        var fighter = new MatchFighter(characterId, team);
        _fighters.Add(fighter);
        return fighter;
    }

    public void Leave(ulong characterId) => _fighters.RemoveAll(fighter => fighter.CharacterId == characterId);

    public MatchSummon? Touch(int triggerId)
    {
        if (!_points.ByTrigger.TryGetValue(triggerId, out var row)) return null;
        if (_summons.Values.Any(summon => !summon.Cleared)) return null;
        var summon = new MatchSummon(triggerId, row.MonsterId);
        _summons[triggerId] = summon;
        return summon;
    }

    public IReadOnlyList<(MatchSummon Summon, int Team, ushort Monster)> Tick(int elapsedMs)
    {
        _tickCarryMs += elapsedMs;
        var expired = new List<(MatchSummon, int, ushort)>();
        while (_tickCarryMs >= 1000)
        {
            _tickCarryMs -= 1000;
            foreach (var summon in _summons.Values)
            {
                if (summon.Cleared) continue;
                summon.SecondsLeft--;
                if (summon.SecondsLeft > 0) continue;
                for (var team = 0; team < GuildGameRules.Teams; team++)
                {
                    if (!summon.Dead[team] && summon.Monsters[team] is { } monster) expired.Add((summon, team, monster));
                }
            }
        }
        foreach (var trigger in _summons.Where(pair => pair.Value.Cleared).Select(pair => pair.Key).ToArray()) _summons.Remove(trigger);
        return expired;
    }

    public bool MonsterKilled(ushort monsterEntity, ulong killerId)
    {
        var summon = _summons.Values.FirstOrDefault(candidate => candidate.Monsters.Contains(monsterEntity));
        if (summon is null) return false;
        var monsterTeam = Array.IndexOf(summon.Monsters, monsterEntity);
        summon.Dead[monsterTeam] = true;
        if (_fighters.FirstOrDefault(fighter => fighter.CharacterId == killerId) is { } killer)
        {
            if (killer.Team == monsterTeam && _points.ByMonster(summon.MonsterId) is { } row)
            {
                killer.Score += row.Point;
                TeamScore[killer.Team] += row.Point;
            }
            else
            {
                killer.WrongKills++;
            }
        }
        if (summon.Cleared) _summons.Remove(summon.TriggerId);
        return true;
    }

    public void PlayerKilled(ulong victimId, ulong killerId)
    {
        if (_fighters.FirstOrDefault(fighter => fighter.CharacterId == victimId) is { } victim)
        {
            victim.Deaths++;
            TeamDeaths[victim.Team]++;
        }
        if (_fighters.FirstOrDefault(fighter => fighter.CharacterId == killerId) is { } killer && killer.CharacterId != victimId)
        {
            killer.Kills++;
            TeamKills[killer.Team]++;
        }
    }

    public MatchEnd? InstantEnd()
    {
        for (var team = 0; team < GuildGameRules.Teams; team++)
        {
            if (ScoreGoal > 0 && TeamScore[team] >= ScoreGoal) return new MatchEnd(GuildGameRules.WinnerCode(team), GuildGameRules.ResultDecided, GuildGameRules.ReasonScoreGoal);
        }
        for (var team = 0; team < GuildGameRules.Teams; team++)
        {
            if (_fighters.All(fighter => fighter.Team != team) && _fighters.Any(fighter => fighter.Team != team))
            {
                return new MatchEnd(GuildGameRules.WinnerCode(1 - team), GuildGameRules.ResultDecided, GuildGameRules.ReasonNoFighters);
            }
        }
        return null;
    }

    public MatchEnd TimeUp()
    {
        var leader = Leader(TeamScore, higherWins: true) ?? Leader(TeamKills, higherWins: true) ?? Leader(TeamDeaths, higherWins: false);
        if (leader is { } team) return new MatchEnd(GuildGameRules.WinnerCode(team), GuildGameRules.ResultDecided, GuildGameRules.ReasonTimeUp);
        var anyScore = TeamScore.Any(score => score != 0);
        return new MatchEnd(GuildGameRules.WinnerDraw, anyScore ? GuildGameRules.ResultDraw : GuildGameRules.ResultNoScore, GuildGameRules.ReasonTimeUp);
    }

    private static int? Leader(int[] values, bool higherWins)
    {
        if (values[0] == values[1]) return null;
        return (values[0] > values[1]) == higherWins ? 0 : 1;
    }
}
