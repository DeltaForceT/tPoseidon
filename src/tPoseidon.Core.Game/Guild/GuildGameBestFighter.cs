namespace tPoseidon.Core.Game.Guild;

public readonly record struct FighterStanding(ulong CharacterId, int Kills, int Deaths, int Score, int WrongKills, int Level, int Exp);

public static class GuildGameBestFighter
{
    private const int NoLevelYet = 100000;
    private const int NoDeathsYet = 100000;
    private const int NoWrongKillsYet = 100000;

    public static ulong? Choose(byte gameType, IEnumerable<FighterStanding> winners)
    {
        ArgumentNullException.ThrowIfNull(winners);
        return gameType switch
        {
            GuildGameRules.TeamMatch => ChooseByKillsThenDeaths(winners),
            GuildGameRules.DeathMatch => ChooseByKills(winners),
            GuildGameRules.MonsterMatchType => ChooseByScore(winners),
            _ => null,
        };
    }

    private static ulong? ChooseByKillsThenDeaths(IEnumerable<FighterStanding> winners)
    {
        FighterStanding? best = null;
        var kills = 0;
        var deaths = NoDeathsYet;
        var level = NoLevelYet;
        var exp = 0;
        foreach (var fighter in winners)
        {
            var better = fighter.Kills != kills ? fighter.Kills > kills
                : fighter.Deaths != deaths ? fighter.Deaths < deaths
                : fighter.Level != level ? fighter.Level < level
                : fighter.Exp > exp;
            if (!better) continue;
            best = fighter;
            kills = fighter.Kills;
            deaths = fighter.Deaths;
            level = fighter.Level;
            exp = fighter.Exp;
        }
        return best?.CharacterId;
    }

    private static ulong? ChooseByKills(IEnumerable<FighterStanding> winners)
    {
        FighterStanding? best = null;
        var kills = 0;
        var level = NoLevelYet;
        foreach (var fighter in winners)
        {
            var better = fighter.Kills != kills ? fighter.Kills > kills
                : fighter.Level != level ? fighter.Level < level
                : fighter.Exp > level;
            if (!better) continue;
            best = fighter;
            kills = fighter.Kills;
            level = fighter.Level;
        }
        return best?.CharacterId;
    }

    private static ulong? ChooseByScore(IEnumerable<FighterStanding> winners)
    {
        FighterStanding? best = null;
        var score = 0;
        var killsFloor = 0;
        var deathsCeiling = NoDeathsYet;
        var wrongKills = NoWrongKillsYet;
        var level = NoLevelYet;
        var exp = 0;
        foreach (var fighter in winners)
        {
            if (fighter.Score < score) continue;
            if (fighter.Score == score)
            {
                if (fighter.Kills < killsFloor) continue;
                if (fighter.Kills == killsFloor)
                {
                    if (fighter.Deaths > deathsCeiling) continue;
                    if (fighter.Deaths == deathsCeiling)
                    {
                        if (fighter.WrongKills > wrongKills) continue;
                        if (fighter.WrongKills == wrongKills)
                        {
                            if (fighter.Level > level) continue;
                            if (fighter.Level == level && fighter.Exp < exp) continue;
                        }
                    }
                }
            }
            best = fighter;
            score = fighter.Score;
            deathsCeiling = fighter.Kills;
            killsFloor = fighter.Deaths;
            wrongKills = fighter.WrongKills;
            level = fighter.Level;
            exp = fighter.Exp;
        }
        return best?.CharacterId;
    }
}
