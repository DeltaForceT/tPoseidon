namespace tPoseidon.Core.Game.Skills.Effects;

public sealed class EffectHandoff
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, (DateTimeOffset At, ushort OwnerEntityId, IReadOnlyList<SkillEffect> Effects)> _held = new();

    public static bool Carries(SkillEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        var kind = effect.Kind;
        return effect.IsApplied
            && !kind.OnTarget
            && kind.ChangesStat
            && kind.Periodic == SkillPeriodicHit.None
            && kind.Morph == MorphRule.None
            && !kind.HidesOwner
            && !kind.IsAuraChild
            && effect.EsClass >= 0
            && effect.FixedDeltas.Count == 0
            && effect.RemainingMs > 0;
    }

    public void Store(ulong characterId, ushort ownerEntityId, IEnumerable<SkillEffect> effects, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(effects);
        var carried = effects.Where(Carries).ToList();
        lock (_gate)
        {
            if (carried.Count == 0) _held.Remove(characterId);
            else _held[characterId] = (now, ownerEntityId, carried);
        }
    }

    public IReadOnlyList<SkillEffect> Take(
        ulong characterId, DateTimeOffset now, TimeSpan timeToLive, ushort ownerEntityId, Func<ushort> newObjectId)
    {
        ArgumentNullException.ThrowIfNull(newObjectId);
        (DateTimeOffset At, ushort OwnerEntityId, IReadOnlyList<SkillEffect> Effects) held;
        lock (_gate)
        {
            if (!_held.Remove(characterId, out held)) return Array.Empty<SkillEffect>();
        }
        var away = now - held.At;
        if (away > timeToLive) return Array.Empty<SkillEffect>();
        var awayMs = (int)Math.Max(0, away.TotalMilliseconds);
        var restored = new List<SkillEffect>();
        foreach (var effect in held.Effects)
        {
            var remaining = effect.RemainingMs - awayMs;
            if (remaining <= 0) continue;
            restored.Add(new SkillEffect
            {
                ObjectId = newObjectId(),
                Kind = effect.Kind,
                SkillId = effect.SkillId,
                EsClass = effect.EsClass,
                CasterEntityId = effect.CasterEntityId == held.OwnerEntityId ? ownerEntityId : effect.CasterEntityId,
                OwnerEntityId = ownerEntityId,
                Amount = effect.Amount,
                Action = effect.Action,
                Flat = effect.Flat,
                Extra = effect.Extra,
                SecondAmount = effect.SecondAmount,
                ThirdAmount = effect.ThirdAmount,
                TailFloat = effect.TailFloat,
                TailStrings = effect.TailStrings,
                Charges = effect.Charges,
                Slow = effect.Slow,
                SetSpeed = effect.SetSpeed,
                Threshold = effect.Threshold,
                Magnitude = effect.Magnitude,
                TickMs = effect.TickMs,
                DurationMs = remaining,
                Mode = effect.Mode,
            });
        }
        return restored;
    }
}
