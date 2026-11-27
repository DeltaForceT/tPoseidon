using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Skills;

public static class SkillUseStyle
{
    public const int FirstBit = 0x1000;
    public const int BitCount = 6;
    public const ushort FirstRefusalCode = 0x10;
    public const int MagicLock = 0x2000;

    public static ushort? Refusal(int useStyle, int lockedStyles)
    {
        var bit = FirstBit;
        for (var index = 0; index < BitCount; index++, bit <<= 1)
        {
            if ((useStyle & lockedStyles & bit) != 0) return (ushort)(FirstRefusalCode + index);
        }
        return null;
    }

    public static int LockedBy(IEnumerable<SkillEffect> held)
    {
        ArgumentNullException.ThrowIfNull(held);
        var locked = 0;
        foreach (var effect in held)
        {
            if (effect.IsApplied) locked |= effect.Kind.UseStyleLock;
        }
        return locked;
    }
}
