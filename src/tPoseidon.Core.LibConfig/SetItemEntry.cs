namespace tPoseidon.Core.LibConfig;

public sealed record SetItemEntry(int Id, IReadOnlyList<int> Items, IReadOnlyList<int> Bonuses, string Effect);
