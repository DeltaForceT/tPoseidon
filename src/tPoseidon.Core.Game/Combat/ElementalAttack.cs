namespace tPoseidon.Core.Game.Combat;

public readonly record struct ElementalDamage(long Damage, int ElementMask);

public static class ElementalAttack
{
    public const int SlotCount = 9;
    public const int MeleeSlotCount = 8;

    private const double PercentScale = 0.009999999776482582;

    public static ElementalDamage Apply(
        long damage,
        IReadOnlyList<int> attackSlots,
        Func<int, int> resistOfElement,
        int physicalResist)
    {
        ArgumentNullException.ThrowIfNull(attackSlots);
        ArgumentNullException.ThrowIfNull(resistOfElement);

        long adjustment = 0;
        var mask = 0;
        for (var slot = 0; slot < SlotCount && slot < attackSlots.Count; slot++)
        {
            var attack = attackSlots[slot];
            if (attack == 0) continue;
            var element = 1 << slot;
            var bonus = Scale(damage * attack);
            var cut = Scale(bonus * resistOfElement(element));
            adjustment += bonus - cut;
            mask |= element;
        }
        adjustment -= Scale(damage * physicalResist);
        return new ElementalDamage(Math.Max(0L, damage + adjustment), mask);
    }

    public static long[] ResistCuts(long damage, IReadOnlyList<int> attackSlots, Func<int, int> resistOfElement)
    {
        ArgumentNullException.ThrowIfNull(attackSlots);
        ArgumentNullException.ThrowIfNull(resistOfElement);

        var cuts = new long[MeleeSlotCount];
        for (var slot = 0; slot < MeleeSlotCount && slot < attackSlots.Count; slot++)
        {
            var attack = attackSlots[slot];
            if (attack == 0) continue;
            cuts[slot] = Scale(Scale(damage * attack) * resistOfElement(1 << slot));
        }
        return cuts;
    }

    private static long Scale(long value) => (long)(value * PercentScale);
}
