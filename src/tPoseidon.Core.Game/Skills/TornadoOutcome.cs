namespace tPoseidon.Core.Game.Skills;

public sealed record TornadoOutcome(IReadOnlyList<(int Damage, int Kind, ushort Third)> Slots, int Sum, int Landed, int ShieldHits, bool Shielded)
{
    public int ResultCount => Shielded ? ShieldHits : Landed;
}

public readonly record struct TornadoShields(int Gauge, int Bunker, int GuardCount)
{
    public bool Any => Gauge != 0 || Bunker != 0 || GuardCount != 0;
}
