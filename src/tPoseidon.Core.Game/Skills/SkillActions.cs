using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills;

public static class SkillActions
{
    public const int Punch = 1296;
    public const int FairyOfMagic = 1297;
    public const int StoneThrowing = 1298;
    public const int WarmingUp = 1299;
    public const int ManaArrow = 1082;
    public const int TornadoBomb = 1103;
    public const int Cure = 1085;
    public const int TornadoHitCount = 3;
    public const int ArrowRush = 2007;
    public const int ManaArrowMagicLoss = 0x31;
    public const string ArrowCountColumn = "Count";
    public const string ArrowPowerColumn = "AP";

    public const int NoElement = 0;
    public const int FireElement = 1;
    public const int WaterElement = 2;
    public const int WindElement = 4;
    public const int EarthElement = 8;
    public const int ElectricElement = 0x10;
    public const int LightElement = 0x20;
    public const int DarkElement = 0x40;
    public const int NoPropertyElement = 0x80;
    public const int GunElement = 0x100;
    public const int ShadowElement = 0x400;

    public const int LevelCap = 30;
    public const int ExperienceDivisor = 4;
    public const int AttackStartDelayMs = 500;
    public const int AttackResumeDelayMs = 200;
    public const int GunItemSubType = 0x36;

    public const int RefusedMonsterType = 8;
    public const int OtherRefusedMonsterType = 12;

    public const string ApplyRatioColumn = "ApplyRatio";
    public const string LevelRatioColumn = "Ratio1";
    public const string FlatPowerColumn = "Ratio2";

    public static bool IsBuilt(int actionId) =>
        actionId is Punch or FairyOfMagic or StoneThrowing or WarmingUp or ManaArrow or SkillAreaRules.ManaRing or TornadoBomb or Cure
        || SkillHitShapes.Find(actionId) is not null || SkillAreaShapes.Find(actionId) is not null
        || PolarBearHits.CountOf(actionId) > 0 || ChainHits.IsChain(actionId) || SupportSkills.TargetsPlayers(actionId) || GroundSkills.IsGround(actionId) || GunSkills.NeedsGun(actionId) || ThrowSkills.IsThrow(actionId)
        || ObjectHits.IsObjectHit(actionId) || EventSkills.IsBuilt(actionId);

    public static int EnhancerOf(int actionId) =>
        actionId switch
        {
            ManaArrow => ArrowRush,
            SkillAreaRules.ManaRing => SkillAreaRules.ManaRingBooster,
            _ => SkillHitShapes.Find(actionId)?.EnhancerSkillId ?? 0,
        };

    public static int HitsOf(int actionId, SkillTableRow? enhancer) =>
        actionId == ManaArrow ? Math.Max(1, enhancer?.Amount(ArrowCountColumn) ?? 1) : 1;

    public static bool StartsAttackAfterwards(int actionId, bool holdsGun) => actionId switch
    {
        Punch or WarmingUp or TornadoBomb => true,
        StoneThrowing => !holdsGun,
        _ => SkillHitShapes.Find(actionId) is { } shape
            && (shape.Restart == SkillRestart.Always || (shape.Restart == SkillRestart.WithoutGun && !holdsGun)),
    };

    public static bool MayTarget(MonsterParamEntry monster)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return monster.Type != RefusedMonsterType && monster.Type != OtherRefusedMonsterType;
    }

    public static int ElementOf(int actionId) =>
        actionId is FairyOfMagic or ManaArrow ? NoPropertyElement : SkillHitShapes.Find(actionId)?.Element ?? NoElement;

    public static int ResistOf(MonsterParamEntry monster, int element)
    {
        ArgumentNullException.ThrowIfNull(monster);
        return element switch
        {
            NoElement => monster.PhysicalR,
            FireElement => monster.FireR,
            WaterElement => monster.WaterR,
            WindElement => monster.WindR,
            EarthElement => monster.EarthR,
            ElectricElement => monster.ElecR,
            LightElement => monster.LightR,
            DarkElement => monster.DarkR,
            NoPropertyElement => monster.NoPropR,
            GunElement => monster.GunR,
            ShadowElement => monster.ShadowR,
            _ => 0,
        };
    }

    public static SkillHit Resolve(
        int actionId, SkillTableRow action, SkillAttacker attacker, SkillTarget target, Random rng, SkillTableRow? enhancer = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (actionId == ManaArrow) return ResolveArrow(action, enhancer, attacker, target, rng);
        if (SkillHitShapes.Find(actionId) is { } shape)
        {
            return SkillHitShapes.Resolve(shape, action, enhancer, attacker, target, rng);
        }
        var applyRatio = action.Value(ApplyRatioColumn) ?? 0f;
        var flatPower = action.Amount(FlatPowerColumn) ?? 0;
        if (actionId == FairyOfMagic)
        {
            return SkillDamage.Magic(
                attacker, attacker.Ap + flatPower, target, applyRatio, 0, NoPropertyElement, rng);
        }

        var levelRatio = action.Amount(LevelRatioColumn) ?? 0;
        var power = attacker.Ap + Math.Min(attacker.Level, LevelCap) * levelRatio + flatPower;
        return SkillDamage.Physical(attacker, power, target, applyRatio, NoElement, rng);
    }

    public static SkillHit ResolveArrow(
        SkillTableRow action, SkillTableRow? arrowRush, SkillAttacker attacker, SkillTarget target, Random rng)
    {
        ArgumentNullException.ThrowIfNull(action);
        var bonus = arrowRush?.Value(SkillHitShapes.RatioColumn) ?? 0f;
        var coefficient = arrowRush?.Value(SkillHitShapes.ApplyRatioColumn) ?? 0f;
        if (!(bonus > 0f))
        {
            bonus = 1f;
            coefficient = 1f;
        }
        var power = WireLimits.Saturate(Math.Truncate((action.Amount(ArrowPowerColumn) ?? 0) * (double)bonus));
        return SkillDamage.Magic(attacker, power, target, coefficient, ManaArrowMagicLoss, NoPropertyElement, rng);
    }

    public const ushort TornadoSlotEnd = 0xFFFF;
    public const int ShieldAbsorbedKind = 8;
    public const int GuardAbsorbedKind = 6;

    public static TornadoOutcome ResolveTornado(
        SkillTableRow action, SkillAttacker attacker, SkillTarget target, Random rng, TornadoShields shields = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var power = attacker.Ap + (action.Amount(ArrowPowerColumn) ?? 0);
        var coefficient = action.Value(ApplyRatioColumn) ?? 0f;
        var hits = new List<SkillHit>(TornadoHitCount);
        for (var hitIndex = 0; hitIndex < TornadoHitCount; hitIndex++)
        {
            var hit = SkillDamage.Physical(attacker, power, target, coefficient, NoElement, rng);
            var counts = hit.Damage > 0 && hit.Kind != SkillDamage.KindMiss && hit.Kind != TornadoUncountedKind;
            hits.Add(counts ? hit with { Damage = hit.Damage / TornadoHitCount } : hit);
        }
        return RecordTornadoHits(hits, shields);
    }

    public static TornadoOutcome RecordTornadoHits(IReadOnlyList<SkillHit> hits, TornadoShields shields)
    {
        ArgumentNullException.ThrowIfNull(hits);
        var gauge = shields.Gauge;
        var bunker = shields.Bunker;
        var guards = shields.GuardCount;
        var slots = new List<(long Damage, int Kind, ushort Third)>(hits.Count);
        long sum = 0;
        var landed = 0;
        var shieldHits = 0;
        foreach (var hit in hits)
        {
            if (hit.Damage <= 0 || hit.Kind == SkillDamage.KindMiss || hit.Kind == TornadoUncountedKind)
            {
                slots.Add((0, hit.Kind, TornadoSlotEnd));
                continue;
            }
            sum += hit.Damage;
            landed++;
            if (gauge == 0 && bunker == 0 && guards == 0)
            {
                slots.Add((hit.Damage, hit.Kind, TornadoSlotEnd));
                continue;
            }
            shieldHits++;
            if (gauge > 0)
            {
                slots.Add(Absorb(ref gauge, hit));
            }
            else if (bunker > 0)
            {
                slots.Add(Absorb(ref bunker, hit));
            }
            else if (guards > 0)
            {
                sum -= hit.Damage;
                guards--;
                slots.Add((0, GuardAbsorbedKind, (ushort)guards));
            }
            else
            {
                slots.Add((0, 0, 0));
            }
        }
        return new TornadoOutcome(slots, sum, landed, shieldHits, shields.Any);
    }

    private static (long Damage, int Kind, ushort Third) Absorb(ref long left, SkillHit hit)
    {
        if (left >= hit.Damage)
        {
            left -= hit.Damage;
            return (0, ShieldAbsorbedKind, TornadoSlotEnd);
        }
        var through = hit.Damage - left;
        left = 0;
        return (through, hit.Kind, TornadoSlotEnd);
    }

    public const int TornadoUncountedKind = 2;

    public static (long Hp, bool Healed) Heal(long hp, long maxHp, float ratio)
    {
        var raised = WireLimits.Saturate(Math.Truncate(hp + maxHp * (double)ratio));
        var healed = raised > hp && hp < maxHp;
        return (Math.Clamp(raised, 0L, maxHp), healed);
    }

    public static int TmExpForUse(int skillExp, int casterLevel, int targetLevel)
    {
        var difference = targetLevel - casterLevel;
        if (difference < -49 || difference > 49)
        {
            return (skillExp * casterLevel / 10 + skillExp) / 2;
        }
        return (skillExp + skillExp * casterLevel / 10) * (100 - Math.Abs(difference)) / 100;
    }
}
