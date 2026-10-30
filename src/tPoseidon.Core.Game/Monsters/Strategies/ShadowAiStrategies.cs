using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public enum ShadowFollowKind
{
    BattleFollow,
    LazyBattleFollow,
}

public abstract class ShadowAiStrategy : BaseMonsterStrategy
{
    public const int FirstStrategyState = 11;
    public const int EngageIntervalMilliseconds = 500;
    public const int ChaseMilliseconds = 5000;

    public override MonsterAType Atype => MonsterAType.NonresidentMonster;

    public abstract int AiType { get; }

    public int StrategyState => AiType + FirstStrategyState;

    public virtual ShadowFollowKind FollowKind => ShadowFollowKind.BattleFollow;

    public abstract MonsterEntity? ChooseTarget(MonsterEntity self, MonsterEntity? held, IReadOnlyList<MonsterEntity> candidates, DateTimeOffset now);

    protected static MonsterEntity? Nearest(MonsterEntity self, IReadOnlyList<MonsterEntity> candidates, DateTimeOffset now)
    {
        var (selfX, selfY) = self.GetInterpolatedPosition(now);
        MonsterEntity? best = null;
        var bestDistance = double.MaxValue;
        foreach (var candidate in candidates)
        {
            var (x, y) = candidate.GetInterpolatedPosition(now);
            var distance = Math.Pow(x - selfX, 2) + Math.Pow(y - selfY, 2);
            if (distance >= bestDistance) continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }

    protected static bool IsWithinViewRange(MonsterEntity self, MonsterEntity other, DateTimeOffset now)
    {
        var (selfX, selfY) = self.GetInterpolatedPosition(now);
        var (x, y) = other.GetInterpolatedPosition(now);
        return Math.Pow(x - selfX, 2) + Math.Pow(y - selfY, 2) <= Math.Pow(self.AiViewRange, 2);
    }

    protected static MonsterEntity? HeldOrFirst(MonsterEntity? held, IReadOnlyList<MonsterEntity> candidates) =>
        held is { IsDead: false } && candidates.Contains(held) ? held : candidates.Count > 0 ? candidates[0] : null;
}

public sealed class NonresidentMonsterTargetOnlyStrategy : ShadowAiStrategy
{
    public const int TargetOnlyAiType = 0;

    public override string ClassName => nameof(NonresidentMonsterTargetOnlyStrategy);
    public override int AiType => TargetOnlyAiType;

    public override MonsterEntity? ChooseTarget(MonsterEntity self, MonsterEntity? held, IReadOnlyList<MonsterEntity> candidates, DateTimeOffset now) =>
        HeldOrFirst(held, candidates);
}

public sealed class NonresidentMonsterTargetNearStrategy : ShadowAiStrategy
{
    public override string ClassName => nameof(NonresidentMonsterTargetNearStrategy);
    public override int AiType => 1;

    public override MonsterEntity? ChooseTarget(MonsterEntity self, MonsterEntity? held, IReadOnlyList<MonsterEntity> candidates, DateTimeOffset now) =>
        Nearest(self, candidates, now) ?? held;
}

public sealed class NonresidentMonsterTargetNearPosStrategy : ShadowAiStrategy
{
    public override string ClassName => nameof(NonresidentMonsterTargetNearPosStrategy);
    public override int AiType => 2;

    private bool _reached;

    public override MonsterEntity? ChooseTarget(MonsterEntity self, MonsterEntity? held, IReadOnlyList<MonsterEntity> candidates, DateTimeOffset now)
    {
        var current = HeldOrFirst(held, candidates);
        if (current is not null && IsWithinViewRange(self, current, now)) _reached = true;
        if (!_reached) return current;
        return Nearest(self, candidates.Where(candidate => IsWithinViewRange(self, candidate, now)).ToArray(), now) ?? current;
    }
}

public sealed class NonresidentMonsterTargetSkillStrategy : ShadowAiStrategy
{
    public override string ClassName => nameof(NonresidentMonsterTargetSkillStrategy);
    public override int AiType => 3;
    public override ShadowFollowKind FollowKind => ShadowFollowKind.LazyBattleFollow;

    public override MonsterEntity? ChooseTarget(MonsterEntity self, MonsterEntity? held, IReadOnlyList<MonsterEntity> candidates, DateTimeOffset now) =>
        HeldOrFirst(held, candidates);
}
