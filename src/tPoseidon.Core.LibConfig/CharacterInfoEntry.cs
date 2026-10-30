namespace tPoseidon.Core.LibConfig;

public sealed record CharacterInfoEntry
{
        public required int Id { get; init; }

        public string CommonName { get; init; } = string.Empty;

        public string CommonNameEng { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string PartName { get; init; } = string.Empty;
    public string PivotFileName { get; init; } = string.Empty;
    public float WalkSpeed { get; init; }
    public float RunSpeed { get; init; }
    public int MinAtkRange { get; init; }
    public int MaxAtkRange { get; init; }
    public int ChaseRange { get; init; }

        public string DisplayName
    {
        get
        {
            var common = CommonName.Trim();
            if (!string.IsNullOrEmpty(common)) return common;
            var eng = CommonNameEng.Trim();
            if (!string.IsNullOrEmpty(eng)) return eng;
            return "NPC";
        }
    }
}
