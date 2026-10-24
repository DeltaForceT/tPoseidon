namespace tPoseidon.Core.Game.Skills;

public sealed record SkillRider(int PassiveSkill, int EffectAction, bool NeedsLevel);

public static class SkillRiders
{
    public const int Shockwave = 1078;
    public const int DripBomb = 1156;
    public const int Hellfire = 1163;
    public const int Scythe = 1282;
    public const int GravityCrush = 1215;
    public const int Uppercut = 1167;
    public const int ElectricSword = 1470;

    public const int Faint = 1201;
    public const int GuardBreak = 1101;
    public const int WaterWeb = 2112;
    public const int WickedFlame = 2206;
    public const int Fear = 2702;
    public const int WitheringDisease = 2217;
    public const int CounterPunch = 1106;

    public const int PowerStun = 1144;
    public const int DefenseParalysis = 1141;
    public const int WaterNet = 1189;
    public const int CruelFlame = 1178;
    public const int FearOnMonster = 1279;
    public const int UnableRecovery = 1332;
    public const int ElectricSwordStun = 1471;

    public const string StunRatioColumn = "StunRatio";
    public const string PowerColumn = "AP";
    public const int BurnMagicLoss = 0x31;
    public const int CounterPunchFloor = 39;
    public const int WholeBurnMonsterType = 0xD;
    public const float CounterPunchScale = 0.01f;

    private static readonly Dictionary<int, SkillRider[]> Riders = new()
    {
        [Shockwave] = new[] { new SkillRider(Faint, PowerStun, NeedsLevel: false), new SkillRider(GuardBreak, DefenseParalysis, NeedsLevel: false) },
        [DripBomb] = new[] { new SkillRider(WaterWeb, WaterNet, NeedsLevel: true) },
        [Hellfire] = new[] { new SkillRider(WickedFlame, CruelFlame, NeedsLevel: true) },
        [Scythe] = new[] { new SkillRider(Fear, FearOnMonster, NeedsLevel: true) },
        [GravityCrush] = new[] { new SkillRider(WitheringDisease, UnableRecovery, NeedsLevel: false) },
    };

    public static IReadOnlyList<SkillRider> Of(int actionId) => Riders.TryGetValue(actionId, out var riders) ? riders : Array.Empty<SkillRider>();

    public static bool Gates(SkillHit hit, long victimHp) =>
        victimHp > 0 && (hit.Kind & SkillDamage.KindMask) is not (SkillDamage.KindMiss or 2 or 6 or 8);

    public static float CounterPunchCoefficient(long ac) => (float)((ac - CounterPunchFloor) * CounterPunchScale);

    public static bool Stuns(float stunRatio, int roll) => roll <= (int)(stunRatio * 100.0);
}
