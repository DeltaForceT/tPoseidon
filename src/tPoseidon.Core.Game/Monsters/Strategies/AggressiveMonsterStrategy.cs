using tPoseidon.Core.Data;

namespace tPoseidon.Core.Game.Monsters.Strategies;

public sealed class AggressiveMonsterStrategy : BaseMonsterStrategy
{
    public const int AggroScanIntervalMs = 500;

    public override MonsterAType Atype => MonsterAType.AggressiveMonster;
    public override string ClassName => nameof(AggressiveMonsterStrategy);

    public DateTimeOffset NextScanAt { get; private set; }

    public override void OnSpawn(MonsterEntity mob)
    {
        NextScanAt = DateTimeOffset.UtcNow.AddMilliseconds(AggroScanIntervalMs);
    }

    public override void OnTick(MonsterEntity mob, DateTimeOffset now)
    {
        if (now < NextScanAt) return;
        NextScanAt = now.AddMilliseconds(AggroScanIntervalMs);
    }
}
