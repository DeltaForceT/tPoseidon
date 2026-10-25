using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Guardian;

public sealed class GuardianCastRulesTests
{
    private const int Hostile = GuardianCastRules.HostileRelation;
    private const int Support = GuardianCastRules.SupportRelation;

    private static GuardianState Guardian(byte fType, params GuardianSkill[] skills) =>
        new(0x55, 0, 15000, "g", 20, fType, 0, 0, 0, 0, 0, 0, new short[12], 0, 0, 0, skills);

    private static GuardianSkill Card(int id, byte group, byte slot, short level = 1) => new(id, GuardianSkillType.Card, group, level, slot);

    private static GuardianSkill Order(int id, byte slot) => new(id, GuardianSkillType.Order, 0, 1, slot);

    private static int Relation(int skillId) => skillId switch
    {
        >= 5000 and < 6000 => Support,
        >= 4000 and < 5000 => 1,
        _ => Hostile,
    };

    private static SkillTableRow Row(string table, int level, params (string Column, string Value)[] columns)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal) { ["Level"] = level.ToString() };
        foreach (var (column, value) in columns) values[column] = value;
        return new SkillTableRow { Table = table, Level = level, Columns = values };
    }

    private sealed class Scripted : Random
    {
        private readonly Queue<int> _draws;

        public Scripted(params int[] draws)
        {
            _draws = new Queue<int>(draws);
        }

        public override int Next(int maxValue) => _draws.Dequeue();
    }

    [Theory]
    [InlineData(0, 0, 2, 3, 1)]
    [InlineData(1, 1, 3, 2, 0)]
    [InlineData(2, 2, 0, 1, 3)]
    [InlineData(3, 3, 1, 0, 2)]
    public void EachGuardianTypeWalksTheCardGroupsInItsOwnOrder(byte fType, int first, int second, int third, int fourth)
    {
        Assert.Equal<int>([first, second, third, fourth], Enumerable.Range(0, GuardianCastRules.GroupCount).Select(step => GuardianCastRules.GroupAt(fType, step)));
    }

    [Fact]
    public void AGroupOutsideTheRotationIsRefused()
    {
        Assert.Equal(-1, GuardianCastRules.GroupAt(4, 0));
        Assert.Equal(-1, GuardianCastRules.GroupAt(0, 4));
    }

    [Fact]
    public void ClockFiresOnlyAfterMoreThanHalfASecondAndThenRestarts()
    {
        var clock = new GuardianCastClock();

        Assert.False(clock.Due(200));
        Assert.False(clock.Due(300));
        Assert.True(clock.Due(1));
        Assert.False(clock.Due(500));
        Assert.True(clock.Due(1));
    }

    [Fact]
    public void CardCandidatesStartWithTheGuardiansOwnGroupAndKeepSlotOrder()
    {
        var guardian = Guardian(1, Card(1100, 0, 1), Card(1101, 0, 0), Card(1200, 1, 0), Card(1300, 2, 0), Card(1400, 3, 0));

        var ids = GuardianCastRules.CardCandidates(guardian, 1f, Relation, _ => true).Select(candidate => candidate.Skill.SkillId).ToArray();

        Assert.Equal<int>([1200, 1400, 1300, 1101, 1100], ids);
        Assert.All(GuardianCastRules.CardCandidates(guardian, 1f, Relation, _ => true), candidate => Assert.Equal(GuardianCastTarget.Enemy, candidate.Target));
    }

    [Fact]
    public void CardCandidatesSkipSkillsThatAreNotHostile()
    {
        var guardian = Guardian(0, Card(4100, 0, 0), Card(1100, 0, 1));

        Assert.Equal<int>([1100], GuardianCastRules.CardCandidates(guardian, 1f, Relation, _ => true).Select(candidate => candidate.Skill.SkillId));
    }

    [Fact]
    public void CardsWhoseSkillIsNotUsableByAGuardianAreNeverCandidates()
    {
        var guardian = Guardian(0, Card(1100, 0, 0), Card(1101, 0, 1), Card(5100, 2, 0));

        var healthy = GuardianCastRules.CardCandidates(guardian, 1f, Relation, id => id != 1100);
        var weak = GuardianCastRules.CardCandidates(guardian, 0.1f, Relation, id => id != 5100);

        Assert.Equal<int>([1101], healthy.Select(candidate => candidate.Skill.SkillId));
        Assert.Equal<int>([1100, 1101], weak.Select(candidate => candidate.Skill.SkillId));
    }

    [Fact]
    public void BelowAFifthOfTheOwnersLifeSupportSkillsComeFirstAimedAtTheOwner()
    {
        var guardian = Guardian(0, Card(1100, 0, 0), Card(5100, 2, 0));

        var healthy = GuardianCastRules.CardCandidates(guardian, 0.2f, Relation, _ => true).ToArray();
        var weak = GuardianCastRules.CardCandidates(guardian, 0.19f, Relation, _ => true).ToArray();

        Assert.Equal<int>([1100], healthy.Select(candidate => candidate.Skill.SkillId));
        Assert.Equal<int>([5100, 1100], weak.Select(candidate => candidate.Skill.SkillId));
        Assert.Equal(GuardianCastTarget.Owner, weak[0].Target);
        Assert.Equal(GuardianCastTarget.Enemy, weak[1].Target);
    }

    [Fact]
    public void OrderCandidatesFollowTheSlotsAndSkipUsedEntries()
    {
        var guardian = Guardian(0, Order(1100, 2), Order(1101, 0), Order(5100, 1));
        var cycle = new GuardianOrderCycle();
        cycle.Begin(7, guardian.Skills);

        var all = GuardianCastRules.OrderCandidates(guardian, cycle, 1f, Relation).ToArray();
        cycle.MarkUsed(0);
        var rest = GuardianCastRules.OrderCandidates(guardian, cycle, 1f, Relation).ToArray();

        Assert.Equal<int>([1101, 5100, 1100], all.Select(candidate => candidate.Skill.SkillId));
        Assert.Equal<GuardianCastTarget>([GuardianCastTarget.Enemy, GuardianCastTarget.Owner, GuardianCastTarget.Enemy], all.Select(candidate => candidate.Target));
        Assert.Equal<int>([5100, 1100], rest.Select(candidate => candidate.Skill.SkillId));
    }

    [Fact]
    public void TheHeavyHealWaitsUntilTheOwnerIsAtThirtyPercentOrLess()
    {
        var guardian = Guardian(0, Order(GuardianCastRules.HeavyHealSkillId, 0));
        var cycle = new GuardianOrderCycle();
        var heavyHeal = (int id) => Support;

        Assert.Empty(GuardianCastRules.OrderCandidates(guardian, cycle, 0.31f, heavyHeal));
        Assert.Single(GuardianCastRules.OrderCandidates(guardian, cycle, 0.3f, heavyHeal));
    }

    [Fact]
    public void TheOrderRoundStartsAgainWhenEveryEntryWasUsedOrTheTargetChanges()
    {
        var order = new[] { Order(1100, 0), Order(1101, 1) };
        var cycle = new GuardianOrderCycle();
        cycle.Begin(7, order);
        cycle.MarkUsed(0);
        cycle.MarkUsed(1);

        cycle.Begin(7, order);
        Assert.False(cycle.IsUsed(0));

        cycle.MarkUsed(1);
        cycle.Begin(8, order);
        Assert.False(cycle.IsUsed(1));
    }

    [Fact]
    public void HasOrderLooksOnlyAtTheOrderList()
    {
        Assert.False(GuardianCastRules.HasOrder(Guardian(0, Card(1100, 0, 0))));
        Assert.True(GuardianCastRules.HasOrder(Guardian(0, Card(1100, 0, 0), Order(1100, 0))));
    }

    [Theory]
    [InlineData(3f, 6f)]
    [InlineData(0f, 0f)]
    public void TheCooldownIsTwiceTheSkillsCoolTime(float coolTime, float expected)
    {
        Assert.Equal(expected, GuardianCastRules.CooldownSeconds(coolTime));
    }

    [Fact]
    public void ASkillWithoutACoolTimeWaitsTwoSeconds()
    {
        Assert.Equal(2f, GuardianCastRules.CooldownSeconds(null));
    }

    [Fact]
    public void TheGuardiansCooldownRunsForExactlyTheSecondsGiven()
    {
        var cooldowns = new SkillCooldowns();

        cooldowns.Begin(1100, 2f);
        cooldowns.Tick(1999);
        Assert.True(cooldowns.IsRunning(1100));
        cooldowns.Tick(2);
        cooldowns.Tick(1);
        Assert.False(cooldowns.IsRunning(1100));
    }

    [Fact]
    public void AGuardianHitNeverMissesWhereAnEqualAttackerWould()
    {
        var growth = new GuardianGrowthEntry(1, [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f]);
        var points = new short[] { 100, 0, 0, 0, 0, 0, 0, 0, 0, 100, 0, 0 };
        var stats = GuardianStats.Compute(20, points, growth);
        var guardian = GuardianStats.Attacker(20, stats);
        var player = guardian with { AlwaysHits = false };
        var target = new SkillTarget(Dp: 0, Hv: 5000, Md: 0, Lk: 0, Hp: 1000, Level: 20, Resist: 0);

        var guardianHit = SkillDamage.Physical(guardian, 100, target, 1f, 0, new Scripted(99, 99));
        var playerHit = SkillDamage.Physical(player, 100, target, 1f, 0, new Scripted(99, 99));

        Assert.NotEqual(SkillDamage.KindMiss, guardianHit.Kind);
        Assert.Equal(SkillDamage.KindMiss, playerHit.Kind);
    }

    [Fact]
    public void StatsGrowWithTheLevelAndTheGrowthRatiosAndAccuracyAndMagicGetFifty()
    {
        var growth = new GuardianGrowthEntry(1, [1f, 0.5f, 1f, 1f, 0.5f, 1f, 1f, 1f, 1f, 0.85f, 1f, 1f]);
        var points = new short[] { 200, 40, 0, 0, 40, 0, 0, 0, 0, 100, 0, 0 };

        var atTen = GuardianStats.Compute(10, points, growth);
        var atForty = GuardianStats.Compute(40, points, growth);

        Assert.Equal(20, atTen[GuardianStats.ApIndex]);
        Assert.Equal(52, atTen[GuardianStats.AcIndex]);
        Assert.Equal(52, atTen[GuardianStats.MaIndex]);
        Assert.Equal(8, atTen[GuardianStats.HpIndex]);
        Assert.Equal(80, atForty[GuardianStats.ApIndex]);
        Assert.Equal(58, atForty[GuardianStats.AcIndex]);
        Assert.Equal(34, atForty[GuardianStats.HpIndex]);
    }

    [Fact]
    public void ACardCastKillsThroughTheNormalMonsterPathAndTheOwnerGetsTheCredit()
    {
        const ushort ownerEntityId = 0x1111;
        var action = Row("ESAction_Punch", 1, ("Ratio1", "2"), ("Ratio2", "140"), ("ApplyRatio", "0.800000"));
        var growth = new GuardianGrowthEntry(1, [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f]);
        var attacker = GuardianStats.Attacker(40, GuardianStats.Compute(40, [500, 0, 0, 0, 0, 0, 0, 0, 0, 100, 0, 0], growth));
        var monster = new MonsterEntity
        {
            EntityId = 0x2222,
            MapId = 1,
            MonsterParamId = 5,
            Name = "Biter",
            SpawnX = 10,
            SpawnY = 10,
            MaxHp = 30,
            Level = 3,
        };
        monster.InitHp(30);
        var target = new SkillTarget(Dp: 0, Hv: 0, Md: 0, Lk: 0, Hp: monster.CurrentHp, Level: 3, Resist: 0);

        var hit = SkillActions.Resolve(SkillActions.Punch, action, attacker, target, new Scripted(99), null);
        var hpBefore = monster.CurrentHp;
        monster.AddAttacker(ownerEntityId);
        monster.ApplyDamage(hit.Damage);
        if (monster.OwnerEntityId == MonsterEntity.NoOwner) monster.TakeOwner(ownerEntityId, hpBefore);

        Assert.True(hit.Damage >= 30);
        Assert.True(monster.IsDead);
        Assert.Equal(ownerEntityId, monster.OwnerEntityId);
        Assert.Equal(hpBefore, monster.HpWhenOwned);
    }

    [Theory]
    [InlineData(1085, GuardianCastTarget.Owner, GuardianCastKind.Cure)]
    [InlineData(1474, GuardianCastTarget.Owner, GuardianCastKind.None)]
    [InlineData(1320, GuardianCastTarget.Owner, GuardianCastKind.None)]
    [InlineData(1078, GuardianCastTarget.Enemy, GuardianCastKind.Hit)]
    [InlineData(1296, GuardianCastTarget.Enemy, GuardianCastKind.None)]
    [InlineData(1219, GuardianCastTarget.Owner, GuardianCastKind.None)]
    [InlineData(1194, GuardianCastTarget.Enemy, GuardianCastKind.None)]
    [InlineData(1148, GuardianCastTarget.Enemy, GuardianCastKind.None)]
    [InlineData(1082, GuardianCastTarget.Enemy, GuardianCastKind.Hit)]
    [InlineData(1310, GuardianCastTarget.Enemy, GuardianCastKind.Area)]
    [InlineData(1111, GuardianCastTarget.Enemy, GuardianCastKind.ObjectHit)]
    [InlineData(1159, GuardianCastTarget.Enemy, GuardianCastKind.Ground)]
    [InlineData(1258, GuardianCastTarget.Enemy, GuardianCastKind.Chain)]
    [InlineData(1103, GuardianCastTarget.Enemy, GuardianCastKind.Tornado)]
    [InlineData(1086, GuardianCastTarget.Enemy, GuardianCastKind.ManaRing)]
    [InlineData(1110, GuardianCastTarget.Enemy, GuardianCastKind.TargetEffect)]
    [InlineData(1140, GuardianCastTarget.Enemy, GuardianCastKind.None)]
    [InlineData(0, GuardianCastTarget.Enemy, GuardianCastKind.None)]
    public void EachCardActionIsSentToTheSameCastRoutineThePlayersUses(int actionId, GuardianCastTarget target, GuardianCastKind expected)
    {
        Assert.Equal(expected, GuardianCastRules.KindOf(actionId, target));
    }
}
