using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters.Strategies;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public readonly record struct ShadowEngagement(MonsterEntity Shadow, MonsterEntity Target);

public sealed class ShadowTargeting
{
    private readonly Func<int, ShadowAiEntry?> _entryOf;
    private readonly Dictionary<ushort, Memory> _memory = new();

    private sealed class Memory
    {
        public required ShadowAiStrategy Strategy { get; init; }
        public DateTimeOffset NextAt { get; set; }
        public MonsterEntity? Held { get; set; }
    }

    public static bool MayDamage(ShadowAiEntry? entry, bool victimIsPlayer) =>
        entry is not null && (entry.AiType != NonresidentMonsterTargetOnlyStrategy.TargetOnlyAiType || !victimIsPlayer);

    public ShadowTargeting(Func<int, ShadowAiEntry?> entryOf)
    {
        _entryOf = entryOf ?? throw new ArgumentNullException(nameof(entryOf));
    }

    public IReadOnlyList<ShadowEngagement> Due(IReadOnlyList<MonsterEntity> scene, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var engagements = new List<ShadowEngagement>();
        var alive = scene.Where(monster => !monster.IsDead).ToArray();
        foreach (var gone in _memory.Keys.Where(id => alive.All(monster => monster.EntityId != id)).ToArray()) _memory.Remove(gone);
        foreach (var shadow in alive)
        {
            if (_entryOf(shadow.MonsterParamId) is not { } entry) continue;
            if (!_memory.TryGetValue(shadow.EntityId, out var memory))
            {
                if (MonsterStrategyFactory.CreateShadow(entry.AiType) is not { } strategy) continue;
                memory = new Memory { Strategy = strategy, NextAt = now };
                _memory[shadow.EntityId] = memory;
            }
            if (now < memory.NextAt) continue;
            memory.NextAt = now.AddMilliseconds(ShadowAiStrategy.EngageIntervalMilliseconds);
            var candidates = alive.Where(monster => monster.MonsterParamId == entry.TargetId && monster.EntityId != shadow.EntityId).ToArray();
            memory.Held = memory.Strategy.ChooseTarget(shadow, memory.Held, candidates, now);
            if (memory.Held is { } target) engagements.Add(new ShadowEngagement(shadow, target));
        }
        return engagements;
    }
}
