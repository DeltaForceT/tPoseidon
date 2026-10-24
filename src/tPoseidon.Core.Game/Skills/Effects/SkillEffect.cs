using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Skills.Effects;

public enum SkillEffectMode : byte
{
    Applied = 1,
    Failed = 2,
    Resisted = 3,
}

public enum SkillEffectTick
{
    None,
    Warning,
    Ended,
}

public sealed class SkillEffect
{
    public const int WarningLeftMs = 10000;
    public const int MillisecondsPerSecond = 1000;

    public required ushort ObjectId { get; init; }
    public required SkillEffectKind Kind { get; init; }
    public required int SkillId { get; init; }
    public required sbyte EsClass { get; init; }
    public required ushort CasterEntityId { get; init; }
    public required ushort OwnerEntityId { get; init; }
    public required float Amount { get; init; }
    public float Flat { get; init; }
    public required int DurationMs { get; set; }
    public required SkillEffectMode Mode { get; init; }
    public long Sequence { get; set; }
    public int TickMs { get; init; }
    public int Charges { get; set; }
    public float Slow { get; init; }
    public double Threshold { get; init; }
    public int Magnitude { get; init; }
    public bool Active { get; set; }
    public double LifeRatio { get; set; }
    public int PeriodCount { get; set; }
    public bool EndRequested { get; private set; }

    public void EndNow() => EndRequested = true;

    public float Extra { get; set; }
    public float SecondAmount { get; init; }
    public float ThirdAmount { get; init; }
    public ushort BlockId { get; set; }
    public (ushort X, ushort Y)? Point { get; init; }
    public HashSet<ushort> Tracked { get; } = new();
    public int ResistLoss { get; set; }
    public float TailFloat { get; init; }
    public DateTimeOffset RefreshedAt { get; set; }
    public IReadOnlyList<(int Stat, int Delta)> FixedDeltas { get; set; } = Array.Empty<(int Stat, int Delta)>();
    public IReadOnlyList<byte[]> TailStrings { get; init; } = Array.Empty<byte[]>();
    public HarconPlan? SlotPlan { get; set; }
    public IReadOnlyList<(byte Kind, int Delta)> ResistDeltas { get; set; } = Array.Empty<(byte Kind, int Delta)>();

    public int RemainingMs => Math.Max(0, DurationMs - ElapsedMs);

    public void Refresh(int remainingMs)
    {
        DurationMs = ElapsedMs + remainingMs;
        if (_leftMs is not null && !Warned) _leftMs = remainingMs;
    }
    public SkillTableRow? Action { get; init; }

    private int _tickedMs;

    public int TakeDueTicks()
    {
        if (TickMs <= 0 || !IsApplied) return 0;
        var due = (Math.Min(ElapsedMs, DurationMs) - _tickedMs) / TickMs;
        if (due <= 0) return 0;
        _tickedMs += due * TickMs;
        return due;
    }

    private int? _leftMs;

    public int ElapsedMs { get; private set; }
    public int LeftMs => _leftMs ?? DurationMs;
    public bool Warned { get; private set; }

    public bool IsApplied => Mode == SkillEffectMode.Applied;

    public SkillEffectTick Tick(int elapsedMs)
    {
        ElapsedMs += elapsedMs;
        if (EndRequested || ElapsedMs >= DurationMs) return SkillEffectTick.Ended;
        if (!IsApplied) return SkillEffectTick.Ended;
        if (Warned) return SkillEffectTick.None;
        _leftMs = LeftMs - elapsedMs;
        if (_leftMs > WarningLeftMs) return SkillEffectTick.None;
        _leftMs = 0;
        Warned = true;
        return WarnsOwner(EsClass) ? SkillEffectTick.Warning : SkillEffectTick.None;
    }

    public static bool WarnsOwner(sbyte esClass) => esClass is not (3 or -1);
}
