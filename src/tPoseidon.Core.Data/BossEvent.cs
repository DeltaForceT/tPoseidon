using System.Collections.Concurrent;

namespace tPoseidon.Core.Data;

public enum BossEventKind
{
    SummonCalled,
    Spoke,
    ChildSpawned,
    ChildDespawned,
    ResistGrown,
    Died,
    HealCast,
    SkillCast,
}

public readonly record struct BossEvent(
    BossEventKind Kind,
    ushort EntityId,
    ushort MapId,
    ushort Element,
    bool NotEarthQuake,
    string Speech,
    ushort ParentId = 0,
    int SkillId = 0,
    int SkillLevel = 0,
    bool CostPaid = false);

public enum BossCastOutcome
{
    Refused = 0,
    Cast = 1,
    CastDelay = 2,
}

public readonly record struct BossCastRequest(BossEventKind Kind, int SkillId, int Level);

public readonly record struct BossCastPlan(bool Allowed, int MpCost, bool Delayed, bool CooldownApplies);

public sealed class BossEventOutbox
{
    private readonly ConcurrentQueue<BossEvent> _events = new();

    public void Enqueue(BossEvent bossEvent) => _events.Enqueue(bossEvent);

    public bool TryTake(out BossEvent bossEvent) => _events.TryDequeue(out bossEvent);
}
