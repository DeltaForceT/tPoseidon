namespace tPoseidon.Core.Game.Items;

public sealed record ExpPotion(float ExpFactor, float TmFactor, DateTimeOffset Until)
{
    public const int Effect = 51;

    public bool IsActive(DateTimeOffset now) => now < Until;

    public static long Scale(long gain, float factor) =>
        gain <= 0 || factor <= 1f ? gain : (long)Math.Min(long.MaxValue, Math.Round(gain * (double)factor));

    public long ScaleExp(long gain, DateTimeOffset now) => IsActive(now) ? Scale(gain, ExpFactor) : gain;

    public long ScaleTm(long gain, DateTimeOffset now) => IsActive(now) ? Scale(gain, TmFactor) : gain;
}
