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
    int SkillLevel = 0);

public sealed class BossEventOutbox
{
    private readonly ConcurrentQueue<BossEvent> _events = new();

    public void Enqueue(BossEvent bossEvent) => _events.Enqueue(bossEvent);

    public bool TryTake(out BossEvent bossEvent) => _events.TryDequeue(out bossEvent);
}
