using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public sealed class NpcIdleCycle
{
    public const byte DirectionAt135Degrees = 0x5F;
    public const byte DirectionAt225Degrees = 0xA0;

    public const int IdleWaitMinMs = 4000;
    public const int IdleWaitMaxMs = 7000;

    private const int ChoiceCount = 3;
    private const int ChoiceFace135 = 0;
    private const int ChoiceFace225 = 1;

    private readonly Func<NpcEntity, NpcIdleProfile> _profileOf;
    private readonly Random _rng;
    private readonly object _gate = new();
    private readonly Dictionary<ushort, Slot> _slots = new();

    public NpcIdleCycle(Func<NpcEntity, NpcIdleProfile> profileOf, Random? rng = null)
    {
        ArgumentNullException.ThrowIfNull(profileOf);
        _profileOf = profileOf;
        _rng = rng ?? new Random();
    }

    public IReadOnlyList<NpcIdleEvent> Tick(IReadOnlyCollection<NpcEntity> npcs, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(npcs);
        if (npcs.Count == 0) return Array.Empty<NpcIdleEvent>();

        var events = new List<NpcIdleEvent>();
        lock (_gate)
        {
            foreach (var npc in npcs)
            {
                if (!_slots.TryGetValue(npc.EntityId, out var slot))
                {
                    _slots[npc.EntityId] = new Slot { Deadline = now + IdleWait() };
                    continue;
                }

                if (now < slot.Deadline) continue;

                if (slot.Acting)
                {
                    slot.Acting = false;
                    slot.Deadline = now + IdleWait();
                    continue;
                }

                var actFor = Act(npc, events);
                if (actFor > TimeSpan.Zero)
                {
                    slot.Acting = true;
                    slot.Deadline = now + actFor;
                }
                else
                {
                    slot.Deadline = now + IdleWait();
                }
            }
        }
        return events;
    }

    public void Forget(ushort entityId)
    {
        lock (_gate)
        {
            _slots.Remove(entityId);
        }
    }

    private TimeSpan Act(NpcEntity npc, List<NpcIdleEvent> events)
    {
        switch (_rng.Next(ChoiceCount))
        {
            case ChoiceFace135:
                events.Add(new NpcFacingEvent(npc.EntityId, DirectionAt135Degrees));
                return TimeSpan.Zero;
            case ChoiceFace225:
                events.Add(new NpcFacingEvent(npc.EntityId, DirectionAt225Degrees));
                return TimeSpan.Zero;
            default:
                return PlayMotion(npc, events);
        }
    }

    private TimeSpan PlayMotion(NpcEntity npc, List<NpcIdleEvent> events)
    {
        var profile = _profileOf(npc);
        var steps = profile.Script?.Steps;
        if (steps is null || steps.Count == 0) return profile.WaitWithoutMotion;

        var step = steps[_rng.Next(steps.Count)];
        events.Add(new NpcMotionEvent(npc.EntityId, step));
        return TimeSpan.FromSeconds(step.AniTime);
    }

    private TimeSpan IdleWait() => TimeSpan.FromMilliseconds(_rng.Next(IdleWaitMinMs, IdleWaitMaxMs));

    private sealed class Slot
    {
        public bool Acting;
        public DateTimeOffset Deadline;
    }
}

public abstract record NpcIdleEvent(ushort EntityId);

public sealed record NpcFacingEvent(ushort EntityId, byte Direction) : NpcIdleEvent(EntityId);

public sealed record NpcMotionEvent(ushort EntityId, NpcActionScriptStep Step) : NpcIdleEvent(EntityId);
