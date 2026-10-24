using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Characters;

public readonly record struct MakeUp(ushort Type, byte Job, byte Hair);

public static class LookChangeRules
{
    public const int Fee = 50000;
    public const int MinimumJob = 9;
    public const int BagTooSmall = 2;
    public const int JobTooLow = 3;
    public const int NotEnoughMoney = 4;
    public const int WhileDisguised = 5;
    public const byte ChangedByAppearance = 1;
    public const byte BaseTier = 1;

    public static int? OpenRefusal(long money, int job, int wornCount, int freeBagSlots, bool disguised)
    {
        if (money < Fee) return NotEnoughMoney;
        if (job < MinimumJob) return JobTooLow;
        if (wornCount > freeBagSlots) return BagTooSmall;
        if (disguised) return WhileDisguised;
        return null;
    }

    public static bool RequestAllowed(long money, int job, int wornCount, int freeBagSlots) =>
        money >= Fee && job >= MinimumJob && wornCount <= freeBagSlots;

    public static MakeUp? MakeUpOf(Character character, int tier)
    {
        ArgumentNullException.ThrowIfNull(character);
        return tier switch
        {
            BaseTier => new MakeUp((ushort)character.Species, (byte)character.Species, (byte)character.Hair),
            OccupationRules.SecondJobTier when character.Job2 != 0 => new MakeUp(character.Type2, character.Job2, (byte)character.Hair2),
            OccupationRules.ThirdJobTier when character.Job3 != 0 => new MakeUp(character.Type3, character.Job3, (byte)character.Hair3),
            _ => null,
        };
    }

    public static ushort[] Looks(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);
        return
        [
            (byte)character.Species, (ushort)character.Species,
            character.Job2, character.Type2,
            character.Job3, character.Type3,
        ];
    }
}
