namespace tPoseidon.Core.LibConfig;

public sealed record ExchangeOffer
{
    public required int Id { get; init; }
    public IReadOnlyList<int> Results { get; init; } = Array.Empty<int>();
    public IReadOnlyList<CompoundRequirement> Requirements { get; init; } = Array.Empty<CompoundRequirement>();
    public int Fee { get; init; }
}
