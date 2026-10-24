namespace tPoseidon.Core.Game.Wedding;

public enum WeddingHallState : byte
{
    Created = 0,
    Gathering = 1,
    Ceremony = 2,
    AfterCeremony = 3,
    Closed = 4,
}

public enum WeddingEventKind
{
    GatheringTimer,
    Scene,
    CeremonyEnded,
    ClosingTimer,
    HallClosed,
}

public readonly record struct WeddingEvent(WeddingEventKind Kind, uint Value);

public static class WeddingScene
{
    public const uint StartMessage = 2;
    public const uint IntroduceOfficator = 3;
    public const uint GroomTurn = 4;
    public const uint GroomStartMessage = 5;
    public const uint GroomStart = 6;
    public const uint GroomWalking = 7;
    public const uint BrideTurn = 8;
    public const uint BrideStartMessage = 9;
    public const uint BrideStart = 10;
    public const uint BrideWalking = 11;
    public const uint CoupleStartUpstage = 12;
    public const uint CoupleUpstageWalking = 13;
    public const uint OfficiateTurn = 14;
    public const uint OfficiateMessage = 15;
    public const uint EventTurn = 16;
    public const uint RingScene = 17;
    public const uint GroomSay = 18;
    public const uint BrideSay = 19;
    public const uint SecondRingScene = 20;
    public const uint CoupleOutTurn = 21;
    public const uint CoupleWalkOutMessage = 22;
    public const uint CoupleStartWalkOut = 23;
    public const uint CoupleWalkingOut = 24;
    public const uint SceneEnd = 25;

    public const int OfficiateLineBase = 5000;
    public const int OfficiateLineDivisor = 45;

    public static IReadOnlyList<(uint Step, int DelayMs)> FixedSteps { get; } = new (uint, int)[]
    {
        (StartMessage, 5000),
        (IntroduceOfficator, 8000),
        (GroomTurn, 7000),
        (GroomStartMessage, 5000),
        (GroomStart, 5000),
        (GroomWalking, 22000),
        (BrideTurn, 5000),
        (BrideStartMessage, 5000),
        (BrideStart, 5000),
        (BrideWalking, 32000),
        (CoupleStartUpstage, 1000),
        (CoupleUpstageWalking, 2500),
        (OfficiateTurn, 5000),
        (OfficiateMessage, 0),
        (EventTurn, 4000),
        (RingScene, 3000),
        (GroomSay, 3000),
        (BrideSay, 5000),
        (SecondRingScene, 5500),
        (CoupleOutTurn, 5000),
        (CoupleWalkOutMessage, 4500),
        (CoupleStartWalkOut, 5000),
        (CoupleWalkingOut, 30000),
        (SceneEnd, 3000),
    };

    public static int OfficiateLineMs(int byteLength) =>
        Math.Max(0, byteLength * OfficiateLineBase / OfficiateLineDivisor - OfficiateLineBase);

    public static bool IsSent(uint step) => step != OfficiateMessage;
}

public sealed class WeddingCeremony
{
    public static readonly TimeSpan GatheringLength = TimeSpan.FromSeconds(300);
    public static readonly TimeSpan ClosingLength = TimeSpan.FromSeconds(1200);

    public static IReadOnlyList<(TimeSpan At, uint Stage)> GatheringStages { get; } = new[]
    {
        (TimeSpan.Zero, 1u), (TimeSpan.FromSeconds(120), 2u), (TimeSpan.FromSeconds(270), 3u),
    };

    public static IReadOnlyList<(TimeSpan At, uint Stage)> ClosingStages { get; } = new[]
    {
        (TimeSpan.Zero, 1u), (TimeSpan.FromSeconds(600), 2u), (TimeSpan.FromSeconds(1140), 3u),
    };

    private readonly object _gate = new();
    private readonly IReadOnlyList<(uint Step, int DelayMs)> _steps;
    private DateTimeOffset _phaseStartedAt;
    private int _nextStage;
    private int _nextStep;
    private DateTimeOffset _nextStepAt;

    public WeddingCeremony(IReadOnlyList<int> officiateLineLengths)
    {
        ArgumentNullException.ThrowIfNull(officiateLineLengths);
        var officiateMs = officiateLineLengths.Sum(WeddingScene.OfficiateLineMs);
        _steps = WeddingScene.FixedSteps
            .Select(step => step.Step == WeddingScene.OfficiateMessage ? (step.Step, officiateMs) : step)
            .ToArray();
    }

    public WeddingHallState State { get; private set; } = WeddingHallState.Created;

    public IReadOnlyList<WeddingEvent> Open(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (State != WeddingHallState.Created) return Array.Empty<WeddingEvent>();
            State = WeddingHallState.Gathering;
            _phaseStartedAt = now;
            _nextStage = 0;
            return Advance(now);
        }
    }

    public IReadOnlyList<WeddingEvent> Tick(DateTimeOffset now)
    {
        lock (_gate) return Advance(now);
    }

    private List<WeddingEvent> Advance(DateTimeOffset now)
    {
        var events = new List<WeddingEvent>();
        var progressed = true;
        while (progressed)
        {
            progressed = false;
            switch (State)
            {
                case WeddingHallState.Gathering:
                    if (_nextStage < GatheringStages.Count && now - _phaseStartedAt >= GatheringStages[_nextStage].At)
                    {
                        events.Add(new WeddingEvent(WeddingEventKind.GatheringTimer, GatheringStages[_nextStage].Stage));
                        _nextStage++;
                        progressed = true;
                    }
                    else if (now - _phaseStartedAt >= GatheringLength)
                    {
                        State = WeddingHallState.Ceremony;
                        _nextStep = 0;
                        _nextStepAt = _phaseStartedAt + GatheringLength;
                        progressed = true;
                    }
                    break;
                case WeddingHallState.Ceremony:
                    if (now < _nextStepAt) break;
                    if (_nextStep < _steps.Count)
                    {
                        var (step, delay) = _steps[_nextStep];
                        events.Add(new WeddingEvent(WeddingEventKind.Scene, step));
                        _nextStepAt += TimeSpan.FromMilliseconds(delay);
                        _nextStep++;
                    }
                    else
                    {
                        events.Add(new WeddingEvent(WeddingEventKind.CeremonyEnded, 0));
                        State = WeddingHallState.AfterCeremony;
                        _phaseStartedAt = _nextStepAt;
                        _nextStage = 0;
                    }
                    progressed = true;
                    break;
                case WeddingHallState.AfterCeremony:
                    if (_nextStage < ClosingStages.Count && now - _phaseStartedAt >= ClosingStages[_nextStage].At)
                    {
                        events.Add(new WeddingEvent(WeddingEventKind.ClosingTimer, ClosingStages[_nextStage].Stage));
                        _nextStage++;
                        progressed = true;
                    }
                    else if (now - _phaseStartedAt >= ClosingLength)
                    {
                        State = WeddingHallState.Closed;
                        events.Add(new WeddingEvent(WeddingEventKind.HallClosed, 0));
                    }
                    break;
            }
        }
        return events;
    }
}
