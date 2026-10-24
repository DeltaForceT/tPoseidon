using System.Text;

namespace tPoseidon.Core.LibConfig;

public sealed record CharacterCombatInfo
{
    public const int DefaultMaxAttackRange = 86;
    public const int SmallestMaxAttackRange = 16;
    public const int MinBelowMaxAttackRange = 15;
    public const int DefaultChaseRange = 200;
    public const int SmallestChaseRange = 16;
    public const int MissingAnimationMs = 1000;
    public const int DelayPerDxPointMs = 100;
    public const int LowestDxModifier = -30;
    public const double NegativeDxFactor = 0.01;

    public const string AttackAnimation = "공격";
    public const string Attack2Animation = "공격2";
    public const string AttackReadyAnimation = "공격준비";

    public required int Id { get; init; }
    public required int MinAttackRange { get; init; }
    public required int MaxAttackRange { get; init; }
    public required int ChaseRange { get; init; }
    public required float WalkSpeed { get; init; }
    public required float RunSpeed { get; init; }
    public required int AttackMs { get; init; }
    public required int Attack2Ms { get; init; }
    public required int AttackReadyMs { get; init; }

    public int? ApproachOverride { get; init; }

    public int ApproachRadius => ApproachOverride ?? (MinAttackRange + MaxAttackRange) / 2;

    public CharacterCombatInfo WithRangeBonus(int bonus) =>
        bonus == 0 ? this : this with { MaxAttackRange = MaxAttackRange + bonus, ChaseRange = ChaseRange + bonus, ApproachOverride = ApproachRadius + bonus };

    public int PlayerSwingDelayMs(int dx) => Adjusted(AttackMs + AttackReadyMs, dx);

    public int PlayerCriticalSwingDelayMs(int dx) => Adjusted(Attack2Ms, dx);

    public int MonsterSwingDelayMs(int dx) => AttackMs + Math.Max(dx, 0) * DelayPerDxPointMs;

    private static int Adjusted(int baseMs, int dx)
        => dx >= 0
            ? baseMs + dx * DelayPerDxPointMs
            : (int)(baseMs * (1 + Math.Max(dx, LowestDxModifier) * NegativeDxFactor));

    public static CharacterCombatInfo From(CharacterInfoEntry entry, IReadOnlyList<NoriAnimation>? animations)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var max = entry.MaxAtkRange < SmallestMaxAttackRange ? DefaultMaxAttackRange : entry.MaxAtkRange;
        var min = entry.MinAtkRange < 1 || entry.MinAtkRange >= max ? max - MinBelowMaxAttackRange : entry.MinAtkRange;
        var chase = entry.ChaseRange < SmallestChaseRange ? DefaultChaseRange : entry.ChaseRange;

        return new CharacterCombatInfo
        {
            Id = entry.Id,
            MinAttackRange = min,
            MaxAttackRange = max,
            ChaseRange = chase,
            WalkSpeed = entry.WalkSpeed,
            RunSpeed = entry.RunSpeed,
            AttackMs = Delay(animations, AttackAnimation),
            Attack2Ms = Delay(animations, Attack2Animation),
            AttackReadyMs = Delay(animations, AttackReadyAnimation),
        };
    }

    public static IReadOnlyList<CharacterCombatInfo> Build(
        IEnumerable<CharacterInfoEntry> entries,
        string clientRoot,
        Encoding nameEncoding)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrEmpty(clientRoot);
        ArgumentNullException.ThrowIfNull(nameEncoding);

        var animationsByFile = new Dictionary<string, IReadOnlyList<NoriAnimation>?>(StringComparer.OrdinalIgnoreCase);
        var infos = new List<CharacterCombatInfo>();
        foreach (var entry in entries)
        {
            IReadOnlyList<NoriAnimation>? animations = null;
            if (!string.IsNullOrWhiteSpace(entry.FileName)
                && !animationsByFile.TryGetValue(entry.FileName, out animations))
            {
                if (tPoseidon.Core.Common.ClientFiles.Locate(tPoseidon.Core.Common.ClientFiles.Combine(clientRoot, entry.FileName)) is { } path)
                {
                    try
                    {
                        animations = NoriAnimationReader.Read(File.ReadAllBytes(path), nameEncoding);
                    }
                    catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException)
                    {
                        animations = null;
                    }
                }
                animationsByFile[entry.FileName] = animations;
            }
            infos.Add(From(entry, animations));
        }
        return infos;
    }

    private static int Delay(IReadOnlyList<NoriAnimation>? animations, string name)
        => animations is null ? 0 : NoriAnimationReader.TotalOf(animations, name) ?? MissingAnimationMs;
}
