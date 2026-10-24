using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Missions;

public enum MissionPhase
{
    Travelling,
    Running,
    Exiting,
}

public static class MissionRules
{
    public const byte CannotProceed = 1;
    public const byte Succeeded = 1;
    public const byte Failed = 0;
    public static readonly TimeSpan ExitDelay = TimeSpan.FromSeconds(0x14);

    public static bool InBand(MissionInfoEntry mission, int level)
    {
        ArgumentNullException.ThrowIfNull(mission);
        return level >= mission.MinLevel && level <= mission.MaxLevel;
    }
}

public sealed class MissionRun
{
    private readonly List<ushort> _waveMonsters = new();

    public MissionRun(MissionInfoEntry mission, IReadOnlyList<MissionWave> waves, ushort returnMap, ushort returnX, ushort returnY)
    {
        Mission = mission ?? throw new ArgumentNullException(nameof(mission));
        Waves = waves ?? throw new ArgumentNullException(nameof(waves));
        ReturnMap = returnMap;
        ReturnX = returnX;
        ReturnY = returnY;
    }

    public MissionInfoEntry Mission { get; }
    public IReadOnlyList<MissionWave> Waves { get; }
    public ushort ReturnMap { get; }
    public ushort ReturnX { get; }
    public ushort ReturnY { get; }
    public MissionPhase Phase { get; private set; } = MissionPhase.Travelling;
    public int WaveIndex { get; private set; } = -1;
    public DateTimeOffset EndsAt { get; private set; }
    public DateTimeOffset ExitAt { get; private set; }
    public byte? Result { get; private set; }
    public IReadOnlyList<ushort> WaveMonsters => _waveMonsters;

    public MissionWave? Start(DateTimeOffset now)
    {
        if (Phase != MissionPhase.Travelling) return null;
        Phase = MissionPhase.Running;
        EndsAt = now + TimeSpan.FromMinutes(Mission.MissionMinutes);
        return NextWave();
    }

    public void Spawned(IEnumerable<ushort> monsters)
    {
        ArgumentNullException.ThrowIfNull(monsters);
        _waveMonsters.AddRange(monsters);
    }

    public MissionWave? WaveCleared(DateTimeOffset now)
    {
        if (Phase != MissionPhase.Running) return null;
        _waveMonsters.Clear();
        var next = NextWave();
        if (next is null) Finish(MissionRules.Succeeded, now);
        return next;
    }

    public bool TimedOut(DateTimeOffset now)
    {
        if (Phase != MissionPhase.Running || now < EndsAt) return false;
        Finish(MissionRules.Failed, now);
        return true;
    }

    public bool ReadyToLeave(DateTimeOffset now) => Phase == MissionPhase.Exiting && now >= ExitAt;

    private MissionWave? NextWave()
    {
        WaveIndex++;
        return WaveIndex < Waves.Count ? Waves[WaveIndex] : null;
    }

    private void Finish(byte result, DateTimeOffset now)
    {
        Result = result;
        Phase = MissionPhase.Exiting;
        ExitAt = now + MissionRules.ExitDelay;
    }
}
