namespace tPoseidon.Core.Game.Monsters;

public readonly record struct Corpse(ushort EntityId, ushort MapId, DateTimeOffset DiedAt);

public sealed class CorpseLedger
{
    public static readonly TimeSpan Hold = TimeSpan.FromMilliseconds(2000);

    private readonly object _gate = new();
    private readonly List<Corpse> _corpses = new();

    public int Count
    {
        get
        {
            lock (_gate) return _corpses.Count;
        }
    }

    public static bool HoldElapsed(DateTimeOffset diedAt, DateTimeOffset now) => now - diedAt > Hold;

    public void Add(ushort entityId, ushort mapId, DateTimeOffset diedAt)
    {
        lock (_gate) _corpses.Add(new Corpse(entityId, mapId, diedAt));
    }

    public IReadOnlyList<Corpse> TakeDue(DateTimeOffset now)
    {
        lock (_gate)
        {
            var due = _corpses.Where(corpse => HoldElapsed(corpse.DiedAt, now)).ToList();
            if (due.Count != 0) _corpses.RemoveAll(corpse => HoldElapsed(corpse.DiedAt, now));
            return due;
        }
    }
}
