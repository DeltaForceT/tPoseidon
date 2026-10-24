using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Combat;

public sealed class ActionManager
{
    private readonly ConcurrentDictionary<int, CombatAction> _byActionId = new();
    private int _nextActionId;

    public CombatAction Create(ushort attackerEntityId, ushort targetEntityId, int skillId)
    {
        var actionId = Interlocked.Increment(ref _nextActionId);
        var action = new CombatAction
        {
            ActionId = actionId,
            AttackerEntityId = attackerEntityId,
            TargetEntityId = targetEntityId,
            SkillId = skillId,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _byActionId[actionId] = action;
        return action;
    }

    public CombatAction? Get(int actionId) =>
        _byActionId.TryGetValue(actionId, out var a) ? a : null;

    public bool Remove(int actionId) => _byActionId.TryRemove(actionId, out _);

    public int ActiveCount => _byActionId.Count;
}

public sealed record CombatAction
{
    public required int ActionId { get; init; }
    public required ushort AttackerEntityId { get; init; }
    public required ushort TargetEntityId { get; init; }
    public required int SkillId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}
