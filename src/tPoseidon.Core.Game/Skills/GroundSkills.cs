namespace tPoseidon.Core.Game.Skills;

public sealed record GroundSpawn(int ObjectAction, int Limit, bool SkipsFlyers, bool AnnouncesPoint);

public static class GroundSkills
{
    public const int PositionInput = 3;
    public const int RegionInput = 6;
    public const int PlacementInput = 10;

    public const int ManaStorm = 1087;
    public const int ManaStormHit = 1088;
    public const int CleavingTerra = 1159;
    public const int EarthDivideHit = 1160;
    public const int Earthquake = 1265;
    public const int EarthquakeDo = 1266;

    public const int DimensionCrack = 1484;
    public const int BuffCancler = 1477;
    public const int DimensionMagicLoss = 0x19;
    public const string KillNumColumn = "KillNum";
    public const string RatioColumn = "Ratio";

    public const int ManaStormVictims = 7;
    public const int EarthquakeVictims = 5;

    private static readonly Dictionary<int, GroundSpawn> Spawns = new()
    {
        [ManaStorm] = new(ManaStormHit, ManaStormVictims, SkipsFlyers: true, AnnouncesPoint: false),
        [CleavingTerra] = new(EarthDivideHit, ManaStormVictims, SkipsFlyers: true, AnnouncesPoint: false),
        [Earthquake] = new(EarthquakeDo, EarthquakeVictims, SkipsFlyers: false, AnnouncesPoint: true),
    };

    public static bool UsesPoint(int targetInput) => targetInput is PositionInput or RegionInput or PlacementInput;

    public static GroundSpawn? SpawnOf(int actionId) => Spawns.TryGetValue(actionId, out var spawn) ? spawn : null;

    public static bool IsGround(int actionId) =>
        Spawns.ContainsKey(actionId) || actionId is DimensionCrack or BuffCancler or ThrowSkills.FourCard || ObjectHits.IsGround(actionId)
        || Effects.SkillEffectKinds.Find(actionId) is { FieldChild: not 0, FollowsCaster: false };
}
