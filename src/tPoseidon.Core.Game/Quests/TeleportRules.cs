using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Quests;

public static class TeleportRules
{
    public const int Allowed = 1;
    public const int LevelTooLow = 2;
    public const int MoneyTooLow = 3;
    public const int FirstZone = 1;
    public const int LastZone = 999;

    public static int Check(TeleportEntry? entry, int level, long money, int index)
    {
        if (entry is null) return NpcScript.Refused;
        if (level < entry.MinLevel) return LevelTooLow;
        if (index < 0 || index >= entry.Destinations.Count) return NpcScript.Refused;
        if (money < entry.Destinations[index].Cost) return MoneyTooLow;
        return Allowed;
    }

    public static bool IsZone(int zone) => zone is >= FirstZone and <= LastZone;
}
