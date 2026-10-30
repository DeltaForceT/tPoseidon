using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Monsters;

public enum PartyZoneClosingKind
{
    Notice,
    Teleport,
}

public readonly record struct PartyZoneClosingEvent(ushort Zone, PartyZoneClosingKind Kind, int Value);

public sealed class PartyZoneClosingTimer
{
    public static readonly IReadOnlyList<int> NoticeSeconds = new[] { 180, 120, 60, 30, 20, 10, 5, 4, 3, 2, 1 };

    private const int MillisecondsPerSecond = 1000;

    private readonly Dictionary<ushort, Zone> _zones = new();
    private readonly object _gate = new();

    public PartyZoneClosingTimer(PartyZoneTables tables, IEnumerable<BossSpawnTableEntry> bossRows)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(bossRows);
        foreach (var rowsOfZone in bossRows.GroupBy(row => row.MapId))
        {
            if (rowsOfZone.Key is < 0 or > ushort.MaxValue || !tables.Closings.TryGetValue(rowsOfZone.Key, out var closing)) continue;
            var rows = rowsOfZone.ToArray();
            _zones[(ushort)rowsOfZone.Key] = new Zone(
                closing,
                rows.Select(row => row.MonsterId).ToHashSet(),
                rows.Sum(row => row.Count));
        }
    }

    public IReadOnlyCollection<ushort> Zones => _zones.Keys;

    public bool IsClosing(ushort zone)
    {
        lock (_gate)
        {
            return _zones.TryGetValue(zone, out var state) && state.ClosingSince is not null;
        }
    }

    public void BossKilled(ushort zone, int monsterId, long nowMs)
    {
        lock (_gate)
        {
            if (!_zones.TryGetValue(zone, out var state) || state.ClosingSince is not null || !state.BossIds.Contains(monsterId)) return;
            state.Killed++;
            if (state.Killed < state.BossTotal) return;
            state.ClosingSince = nowMs;
            state.NextNotice = 0;
        }
    }

    public IReadOnlyList<PartyZoneClosingEvent> Tick(long nowMs, Func<ushort, bool> occupied)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        var events = new List<PartyZoneClosingEvent>();
        lock (_gate)
        {
            foreach (var (zone, state) in _zones)
            {
                if (!occupied(zone))
                {
                    state.Reset();
                    continue;
                }
                if (state.ClosingSince is not long since) continue;
                var left = state.Closing.Seconds - (int)((nowMs - since) / MillisecondsPerSecond);
                if (left <= 0)
                {
                    events.Add(new PartyZoneClosingEvent(zone, PartyZoneClosingKind.Teleport, state.Closing.DestZone));
                    state.Reset();
                    continue;
                }
                if (state.NextNotice >= NoticeSeconds.Count || left > NoticeSeconds[state.NextNotice]) continue;
                if (left == NoticeSeconds[state.NextNotice]) events.Add(new PartyZoneClosingEvent(zone, PartyZoneClosingKind.Notice, left));
                do
                {
                    state.NextNotice++;
                }
                while (state.NextNotice < NoticeSeconds.Count && left <= NoticeSeconds[state.NextNotice]);
            }
        }
        return events;
    }

    private sealed class Zone
    {
        public Zone(PartyZoneClosing closing, HashSet<int> bossIds, int bossTotal)
        {
            Closing = closing;
            BossIds = bossIds;
            BossTotal = bossTotal;
        }

        public PartyZoneClosing Closing { get; }
        public HashSet<int> BossIds { get; }
        public int BossTotal { get; }
        public int Killed { get; set; }
        public long? ClosingSince { get; set; }
        public int NextNotice { get; set; }

        public void Reset()
        {
            Killed = 0;
            ClosingSince = null;
            NextNotice = 0;
        }
    }
}
