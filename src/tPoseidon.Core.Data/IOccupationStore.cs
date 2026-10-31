namespace tPoseidon.Core.Data;

public static class OccupationRules
{
    public const byte SecondJobTier = 2;
    public const byte ThirdJobTier = 3;
    public const int FirstSecondJobType = 10;
    public const int LastSecondJobType = 18;
    public const int FirstThirdJobType = 19;
    public const int LastThirdJobType = 27;

    public static byte? TierOf(int changeType, int changeJob) => changeType switch
    {
        >= FirstSecondJobType and <= LastSecondJobType when changeJob == changeType => SecondJobTier,
        >= FirstThirdJobType and <= LastThirdJobType when changeJob >= FirstThirdJobType => ThirdJobTier,
        _ => null,
    };

    public static int CurrentJobOf(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        if (character.Job3 != 0) return character.Job3;
        return character.Job2 != 0 ? character.Job2 : (int)character.Species;
    }
}

public interface IOccupationStore
{
    bool ChangeOccupation(ulong characterId, byte tier, ushort type, byte job, byte hair);

    bool SetChangeJobQuestState(ulong characterId, byte state);

    bool SetJobState(ulong characterId, byte jobIndex);
}
