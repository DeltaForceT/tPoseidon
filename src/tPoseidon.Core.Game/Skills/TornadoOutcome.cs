namespace tPoseidon.Core.Game.Skills;

public sealed record TornadoOutcome(IReadOnlyList<(long Damage, int Kind, ushort Third)> Slots, long Sum, int Landed, int ShieldHits, bool Shielded)
{
    public int ResultCount => Shielded ? ShieldHits : Landed;
}

public readonly record struct TornadoShields(long Gauge, long Bunker, int GuardCount)
{
    public bool Any => Gauge != 0 || Bunker != 0 || GuardCount != 0;
}
