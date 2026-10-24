namespace tPoseidon.Core.Game.Skills;

public static class DebuffResist
{
    public const int Immune = 100;
    public const int Vulnerable = 0;
    public const int RollRange = 100;

    public static (bool Blocked, uint Mask) Roll(IReadOnlyList<int> values, int debuffBits, Func<int> rollBelowHundred)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(rollBelowHundred);
        var blocked = true;
        var mask = 0u;
        for (var bit = 0; bit < values.Count && bit < 31; bit++)
        {
            if ((debuffBits & (1 << bit)) == 0) continue;
            var value = values[bit];
            if (value == Vulnerable)
            {
                blocked = false;
            }
            else if (value == Immune)
            {
                mask |= 1u << bit;
            }
            else if (rollBelowHundred() < value)
            {
                blocked = false;
            }
            else
            {
                mask |= 1u << bit;
            }
        }
        return (blocked, mask);
    }
}
