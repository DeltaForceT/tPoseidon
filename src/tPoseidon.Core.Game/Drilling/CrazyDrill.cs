using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Drilling;

public static class CrazyDrillRules
{
    public const int Skill = 5019;
    public const int MegaSkill = 3111;
    public const int StartPoint = 0;
    public const int FindPoint = 1;
    public const int EmptyPoint = 2;
    public const int DamagePoint = 3;
    public const float DefaultGaugeMax = 120f;
    public const float InitialStressBonus = 0f;
    public const float DefaultStressBonus = 0.01f;
    public const float DefaultRatioBang = 0.05f;
    public const int DefaultSeconds = 30;
    public const int DefaultDurationMs = 30000;
    public const int DigCooldownMilliseconds = 200;
    public const byte Found = 8;
    public const byte Cooldown = 9;
    public const byte Started = 0x0B;
    public const byte HalfLeft = 0x0C;
    public const byte QuarterLeft = 0x0D;
    public const byte FiveLeft = 0x0E;
    public const byte MapRefuses = 0x0F;
    public const byte LeftByMap = 0x10;
    public const byte DrillChanged = 0x11;
    public const byte AutoPetWorn = 0x12;
    public const int MapOptionNoCrazy = 0x200;
    public const int ExpDepth = 10;
    public const int HalfNoticeMinMs = 40000;
    public const int QuarterNoticeMinMs = 60000;
    public const int LastNoticeBelowMs = 5001;
    public const int LastNoticeSeconds = 5;
    public const int MillisecondsPerSecond = 1000;
    public const int BangTries = 25;
    public const int BangWindow = 7;
    public const int BangReach = 3;
    public const int BangMaxItems = 10;

    public static readonly IReadOnlySet<int> MapTypesThatEndIt = new HashSet<int> { 1, 5, 6 };

    public static float GaugeMax(DrillParamEntry drill)
    {
        ArgumentNullException.ThrowIfNull(drill);
        return drill.CrazyGaugeMax > 0 ? drill.CrazyGaugeMax : DefaultGaugeMax;
    }

    public static float StressBonus(DrillParamEntry drill)
    {
        ArgumentNullException.ThrowIfNull(drill);
        return drill.CrazyStressBonus > 0 ? drill.CrazyStressBonus : DefaultStressBonus;
    }

    public static int Seconds(DrillParamEntry drill)
    {
        ArgumentNullException.ThrowIfNull(drill);
        return drill.CrazySeconds > 0 ? drill.CrazySeconds : DefaultSeconds;
    }

    public static int DurationMs(DrillParamEntry drill, float megaRatio)
    {
        ArgumentNullException.ThrowIfNull(drill);
        var duration = drill.CrazySeconds * MillisecondsPerSecond;
        if (megaRatio > 0f) duration = (int)Math.Truncate(duration * ((double)megaRatio + 1.0));
        return duration < 1 ? DefaultDurationMs : duration;
    }

    public static float RatioBang(DrillParamEntry drill, bool couple)
    {
        ArgumentNullException.ThrowIfNull(drill);
        var ratio = drill.CrazyRatioBang <= 0f || drill.CrazyRatioBang > 1f ? DefaultRatioBang : drill.CrazyRatioBang;
        return couple ? ratio + drill.WeddingRatioBang : ratio;
    }

    public static byte Percent(float gauge, float gaugeMax) =>
        gaugeMax <= 0 ? (byte)0 : (byte)Math.Clamp((int)(gauge / gaugeMax * 100f), 0, 100);

    public static float Point(IReadOnlyList<float> points, int kind)
    {
        ArgumentNullException.ThrowIfNull(points);
        return kind >= 0 && kind < points.Count ? points[kind] : 0f;
    }

    public static int TmExp(int level, int grade) => (level * 12 + 40) * grade;

    public static int EndPoint(int? laidDepth, int rolledItem, int depth, int mapDepth, byte result)
    {
        if (result == DrillResult.Found) return FindPoint;
        if (laidDepth is null) return EmptyPoint;
        return rolledItem == 0 && result == DrillResult.Nothing && depth == mapDepth ? EmptyPoint : -1;
    }

    public static bool Bangs(double roll, float ratio) => roll <= ratio;

    public static IReadOnlyList<(int CellX, int CellY, int ItemId)>? Bang(
        int digCellX, int digCellY, int cellsX, int cellsY, Func<int> randomBelowWindow, Func<int, int, bool> holdsItem, Func<int> popItem)
    {
        ArgumentNullException.ThrowIfNull(randomBelowWindow);
        ArgumentNullException.ThrowIfNull(holdsItem);
        ArgumentNullException.ThrowIfNull(popItem);
        var baseX = digCellX < BangReach + 1 ? 0 : digCellX - BangReach;
        var baseY = digCellY < BangReach + 1 ? 0 : digCellY - BangReach;
        var used = new bool[BangWindow, BangWindow];
        var found = new List<(int CellX, int CellY, int ItemId)>();
        for (var attempt = 0; attempt < BangTries; attempt++)
        {
            var offsetX = randomBelowWindow();
            var offsetY = randomBelowWindow();
            var cellX = baseX + offsetX;
            var cellY = baseY + offsetY;
            if (cellX > cellsX || cellY > cellsY) continue;
            if (holdsItem(cellX, cellY) && !used[offsetX, offsetY])
            {
                var item = popItem();
                if (item == 0) return null;
                found.Add((cellX, cellY, item));
                used[offsetX, offsetY] = true;
            }
            if (found.Count >= BangMaxItems) break;
        }
        return found;
    }
}

public sealed record CrazyDrillCarry(bool Active, int DrillId, int RemainingMs, int HalfMs, int QuarterMs, bool HalfPending, bool QuarterPending, bool LastPending, float Gauge);

public sealed class CrazyDrillMeter
{
    public float Gauge { get; private set; }

    public bool Active { get; private set; }

    public bool StartNoticePending { get; private set; }

    public float GaugeMax { get; private set; } = CrazyDrillRules.DefaultGaugeMax;

    public float StressBonus { get; private set; } = CrazyDrillRules.InitialStressBonus;

    public float RatioBang { get; private set; } = CrazyDrillRules.DefaultRatioBang;

    public bool Couple { get; set; }

    public int DrillId { get; private set; }

    public ulong? DrillUid { get; set; }

    public int DurationMs { get; private set; }

    public int HalfMs { get; private set; }

    public int QuarterMs { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    public DateTimeOffset DigReadyAt { get; set; }

    public bool HalfPending { get; private set; }

    public bool QuarterPending { get; private set; }

    public bool LastPending { get; private set; }

    public int Seconds => DurationMs / CrazyDrillRules.MillisecondsPerSecond;

    public bool Add(DrillParamEntry drill, float points, bool mayBuild, float? boilingStressUp, float megaRatio, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(drill);
        if (!mayBuild || Active || !(points < GaugeMax)) return false;
        if (!Couple && boilingStressUp is { } stressUp)
        {
            Gauge += (stressUp + 1.0f) * (points + StressBonus);
        }
        else
        {
            Gauge += points;
            Gauge += StressBonus;
            if (Couple) Gauge += drill.WeddingStress;
        }
        if (!(Gauge >= GaugeMax)) return false;
        Activate(drill, megaRatio, now);
        return true;
    }

    public byte Percent() => CrazyDrillRules.Percent(Gauge, GaugeMax);

    public void Activate(DrillParamEntry drill, float megaRatio, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(drill);
        Active = true;
        Gauge = 0;
        DigReadyAt = default;
        DrillId = drill.Id;
        GaugeMax = CrazyDrillRules.GaugeMax(drill);
        StressBonus = CrazyDrillRules.StressBonus(drill);
        DurationMs = CrazyDrillRules.DurationMs(drill, megaRatio);
        HalfMs = DurationMs >> 1;
        QuarterMs = DurationMs >> 2;
        StartNoticePending = true;
        if (DurationMs >= CrazyDrillRules.QuarterNoticeMinMs)
        {
            HalfPending = QuarterPending = true;
        }
        else if (DurationMs >= CrazyDrillRules.HalfNoticeMinMs)
        {
            HalfPending = true;
            QuarterPending = false;
        }
        else if (DurationMs >= CrazyDrillRules.HalfNoticeMinMs / 2)
        {
            HalfPending = QuarterPending = false;
        }
        LastPending = true;
        RatioBang = CrazyDrillRules.RatioBang(drill, Couple);
        EndsAt = now.AddMilliseconds(DurationMs);
    }

    public int RemainingMs(DateTimeOffset now) => EndsAt > now ? (int)Math.Ceiling((EndsAt - now).TotalMilliseconds) : 0;

    public void Death()
    {
        Gauge = 0;
        End();
    }

    public void End()
    {
        Active = false;
        StartNoticePending = false;
    }

    public void ResetAll()
    {
        Gauge = 0;
        Active = false;
        StartNoticePending = HalfPending = QuarterPending = LastPending = false;
        GaugeMax = CrazyDrillRules.DefaultGaugeMax;
        StressBonus = CrazyDrillRules.InitialStressBonus;
        RatioBang = CrazyDrillRules.DefaultRatioBang;
        Couple = false;
        DurationMs = HalfMs = QuarterMs = 0;
        DigReadyAt = default;
    }

    public void NoticeSent() => StartNoticePending = false;

    public bool Expired(DateTimeOffset now) => Active && RemainingMs(now) < 1;

    public byte? DueNotice(DateTimeOffset now, out int seconds)
    {
        seconds = 0;
        var remaining = RemainingMs(now);
        if (HalfPending && remaining <= HalfMs)
        {
            HalfPending = false;
            seconds = HalfMs / CrazyDrillRules.MillisecondsPerSecond;
            return CrazyDrillRules.HalfLeft;
        }
        if (QuarterPending && remaining <= QuarterMs)
        {
            QuarterPending = false;
            seconds = QuarterMs / CrazyDrillRules.MillisecondsPerSecond;
            return CrazyDrillRules.QuarterLeft;
        }
        if (LastPending && remaining < CrazyDrillRules.LastNoticeBelowMs)
        {
            LastPending = false;
            seconds = CrazyDrillRules.LastNoticeSeconds;
            return CrazyDrillRules.FiveLeft;
        }
        return null;
    }

    public CrazyDrillCarry Carry(DateTimeOffset now) =>
        new(Active, DrillId, RemainingMs(now), HalfMs, QuarterMs, HalfPending, QuarterPending, LastPending, Gauge);

    public void Restore(CrazyDrillCarry carry, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(carry);
        ResetAll();
        DrillId = carry.DrillId;
        if (carry.Active)
        {
            Active = true;
            DurationMs = carry.RemainingMs;
            HalfMs = carry.HalfMs;
            QuarterMs = carry.QuarterMs;
            HalfPending = carry.HalfPending;
            QuarterPending = carry.QuarterPending;
            LastPending = carry.LastPending;
            EndsAt = now.AddMilliseconds(carry.RemainingMs);
        }
        else
        {
            Gauge = carry.Gauge;
        }
    }
}
