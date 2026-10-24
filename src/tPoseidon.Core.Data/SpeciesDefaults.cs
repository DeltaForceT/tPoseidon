namespace tPoseidon.Core.Data;

public enum Species : byte
{
    Bunny = 1,
    Buffalo = 2,
    Sheep = 3,
    Dragon = 4,
    Fox = 5,
    Lion = 6,
    Cat = 7,
    Raccoon = 8,
    PolarBear = 9,
}

public enum FType : byte
{
    Power = 1,
    Magic = 2,
    Sense = 3,
    Charm = 4,
}

public static class SpeciesDefaults
{
        public static ushort EarItemIndexForSpecies(Species species)
    {
        EnsureKnownSpecies(species);
        return species == Species.PolarBear ? PolarBearEarItemIndex : (ushort)(6900 + ((byte)species - 1));
    }

        public static ushort TailItemIndexForSpecies(Species species)
    {
        EnsureKnownSpecies(species);
        return species == Species.PolarBear ? PolarBearTailItemIndex : (ushort)(6950 + ((byte)species - 1));
    }

    private static void EnsureKnownSpecies(Species species)
    {
        if ((byte)species < 1 || (byte)species > 9)
        {
            throw new ArgumentOutOfRangeException(
                nameof(species), species, "Species must be 1..9 (one of the nine Trickster species, incl. KR2014 PolarBear=9).");
        }
    }

        public const ushort StarterQuickslotStack = 30;

        public const ushort CoralTownSpawnX = 0x03B5;
    public const ushort CoralTownSpawnY = 0x08B4;
    public const ushort CoralTownMapId = 148;

        public static ushort StarterSkillIdForFType(FType fType) => fType switch
    {
        FType.Power => 1012,
        FType.Magic => 2014,
        FType.Sense => 3015,
        FType.Charm => 4013,
        _ => throw new ArgumentOutOfRangeException(nameof(fType), fType, "Unknown FType"),
    };

    public const ushort PolarBearEarItemIndex = 6932;
    public const ushort PolarBearTailItemIndex = 6982;
    public const ushort EquipHammerSkillId = 4701;
    public const ushort EnergeticHammerSkillId = 4709;

    private static readonly ushort[] PolarBearStarterSkills = { EquipHammerSkillId, EnergeticHammerSkillId };

    public static readonly IReadOnlyList<int> PolarBearStarterItems = new[] { 92018 };

    public static IReadOnlyList<ushort> StarterSkillIds(Species species, FType fType) =>
        species == Species.PolarBear ? PolarBearStarterSkills : new[] { StarterSkillIdForFType(fType) };

    public static IReadOnlyList<int> StarterItemIndexes(Species species) =>
        species == Species.PolarBear ? PolarBearStarterItems : Array.Empty<int>();

        public static ushort SecondJobClassIdForFirstJob(Species species) =>
        (ushort)((byte)species + 8);

    public static (byte PtsP, byte PtsM, byte PtsS, byte PtsC) GrowthGraphForFType(FType ftype) => ftype switch
    {
        FType.Power => (LockedPts, BalancedSecondary, BalancedSecondary, BalancedSecondary),
        FType.Magic => (BalancedSecondary, LockedPts, BalancedSecondary, BalancedSecondary),
        FType.Sense => (BalancedSecondary, BalancedSecondary, LockedPts, BalancedSecondary),
        FType.Charm => (BalancedSecondary, BalancedSecondary, BalancedSecondary, LockedPts),
        _ => throw new ArgumentOutOfRangeException(nameof(ftype), ftype, "Unknown FType"),
    };

    public static bool IsValidGrowthGraph(FType ftype, byte ptsP, byte ptsM, byte ptsS, byte ptsC)
    {
        var (locked, a, b, c) = ftype switch
        {
            FType.Power => (ptsP, ptsM, ptsS, ptsC),
            FType.Magic => (ptsM, ptsP, ptsS, ptsC),
            FType.Sense => (ptsS, ptsP, ptsM, ptsC),
            FType.Charm => (ptsC, ptsP, ptsM, ptsS),
            _ => throw new ArgumentOutOfRangeException(nameof(ftype), ftype, "Unknown FType"),
        };
        if (locked != LockedPts) return false;
        if (a < MinSecondary || a > MaxSecondary) return false;
        if (b < MinSecondary || b > MaxSecondary) return false;
        if (c < MinSecondary || c > MaxSecondary) return false;
        if (ptsP + ptsM + ptsS + ptsC != TotalBudget) return false;
        return true;
    }

    public const byte LockedPts = 4;
    public const byte MinSecondary = 1;
    public const byte MaxSecondary = 4;
    public const byte TotalBudget = 10;
    public const byte BalancedSecondary = 2;
}
