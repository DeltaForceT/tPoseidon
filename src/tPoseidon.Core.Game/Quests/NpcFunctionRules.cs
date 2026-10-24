namespace tPoseidon.Core.Game.Quests;

public static class NpcEventTalk
{
    public const int Talk = 0;
    public const int Scene = 1;
}

public sealed record GuildSetupRequirement(int MinLevel, int MinGelder)
{
    public const string TableName = "GuildSetupInfo";
    public const string ServerRow = "MainServer";
    public const int FixedMinLevel = 35;
    public const int FixedMinGelder = 200;
}

public static class GuildSetupRules
{
    public const int Ready = 0;
    public const int LevelLow = 2;
    public const int GelderShort = 3;

    public static int Check(GuildSetupRequirement requirement, int level, long money)
    {
        ArgumentNullException.ThrowIfNull(requirement);
        if (level < requirement.MinLevel) return LevelLow;
        if (money < requirement.MinGelder) return GelderShort;
        if (level < GuildSetupRequirement.FixedMinLevel) return LevelLow;
        if (money < GuildSetupRequirement.FixedMinGelder) return GelderShort;
        return Ready;
    }
}

public static class SympathizingRules
{
    public const int Args = 2;

    public static int Result(int argCount, bool seedPlanted) =>
        argCount != Args ? NpcScript.Refused : seedPlanted ? NpcCheckRules.Allowed : NpcCheckRules.NotAllowed;
}

public static class WarpGateRules
{
    public const int FirstJob = 9;
    public const int LastJob = 33;
    public const int SpeciesRoomBase = 628;
    public const int SpeciesRooms = 8;
    public const int PolarBearRoom = 754;
    public const int PolarBearSpecies = 9;

    public static int? GuardianRoomOfJob(int job) => job switch
    {
        9 or 17 or 18 => 239,
        10 or 19 or 20 => 240,
        11 or 21 or 22 => 241,
        12 or 23 or 24 or 25 => 242,
        13 or 26 or 27 => 243,
        14 or 28 or 29 => 244,
        15 or 30 or 31 => 245,
        16 or 32 or 33 => 246,
        _ => null,
    };

    public static int? GuardianRoomOfSpecies(int job, int species)
    {
        if (job < FirstJob || job > LastJob) return null;
        if (species == PolarBearSpecies) return PolarBearRoom;
        return species is >= 1 and <= SpeciesRooms ? SpeciesRoomBase + species - 1 : null;
    }
}

public static class MonsterSummonRules
{
    public const string TableName = "NpcSummonMonsterTable";
    public const int Columns = 2;
    public const int Args = 2;
    public const int TargetingMapClass = 2;

    public static bool HasRoom(int monstersOnMap, int averageMonsters) => monstersOnMap < averageMonsters;

    public static bool TargetsSummoner(int mapClass) => mapClass == TargetingMapClass;
}

public static class NpcSkillRules
{
    public const uint Function = 258;
    public const int Args = 1;
}

public static class PartyZoneRules
{
    public const int Args = 2;
    public const int Open = 1;
    public const int Closed = 2;
    public const int SecondsPerMinute = 60;

    public static int? MinutesLeft(int reEnterMinutes, IReadOnlySet<int> bosses, IEnumerable<(int MonsterId, DateTimeOffset LatestKill)> kills, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(bosses);
        ArgumentNullException.ThrowIfNull(kills);
        foreach (var (monsterId, latestKill) in kills)
        {
            if (!bosses.Contains(monsterId)) continue;
            var minutes = (int)Math.Floor((now - latestKill).TotalSeconds) / SecondsPerMinute;
            if (minutes < reEnterMinutes) return reEnterMinutes - minutes;
        }
        return null;
    }
}

public static class LimitTeleportRules
{
    public const int Args = 4;
    public const int DestinationArg = 0;
    public const int OkMessageArg = 1;
    public const int FullMessageArg = 2;
    public const int NoMoneyMessageArg = 3;

    public static int? UserLimit(uint functionId) => functionId switch
    {
        88 => 300,
        110 => 50,
        227 => 30,
        _ => null,
    };

    public static bool HasRoom(int? usersInZone, int limit) => usersInZone is int users && users < limit;
}
