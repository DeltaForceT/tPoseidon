namespace tPoseidon.Core.Game.Combat;

public static class MonsterHits
{
    public const int OneHit = 1;

    public static readonly IReadOnlyDictionary<int, int> KnownPerSwing = new Dictionary<int, int>();

    public static int PerSwing(int monsterId, int? overridden = null) =>
        Math.Max(OneHit, overridden ?? (KnownPerSwing.TryGetValue(monsterId, out var hits) ? hits : OneHit));
}
