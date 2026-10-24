using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Progression;

public readonly record struct HonorTitleGrant(ushort TitleId, uint QuestId, byte QuestType, ushort Elapsed, ushort Hunt);

public readonly record struct ProfileTitle(ushort TitleType, ulong TitleId)
{
    public static readonly ProfileTitle None = new(0, 0);
}

public sealed class HonorTitleBook
{
    private readonly Dictionary<int, HonorTitleEntry> _byId = new();
    private readonly Dictionary<(int QuestType, int QuestId), List<HonorTitleEntry>> _byQuest = new();

    public HonorTitleBook(IEnumerable<HonorTitleEntry> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        foreach (var row in rows)
        {
            if (!_byId.TryAdd(row.Id, row)) continue;
            var key = (row.QuestType, row.QuestId);
            if (!_byQuest.TryGetValue(key, out var list)) _byQuest[key] = list = new List<HonorTitleEntry>();
            list.Add(row);
        }
    }

    public static HonorTitleBook Empty { get; } = new(Array.Empty<HonorTitleEntry>());

    public int Count => _byId.Count;

    public bool Exists(int titleId) => _byId.ContainsKey(titleId);

    public IReadOnlyList<HonorTitleGrant> Grants(QuestClear clear, HonorTitleList owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (!_byQuest.TryGetValue((clear.QuestType, clear.QuestId), out var rows)) return Array.Empty<HonorTitleGrant>();
        var grants = new List<HonorTitleGrant>();
        foreach (var row in rows)
        {
            if (owned.Contains((ushort)row.Id)) continue;
            if (HonorTitleRules.Earned(row, clear) is { } grant) grants.Add(grant);
        }
        return grants;
    }
}

public static class HonorTitleRules
{
    public const int MaxOwned = 60;

    public static HonorTitleGrant? Earned(HonorTitleEntry row, QuestClear clear)
    {
        ArgumentNullException.ThrowIfNull(row);
        bool earned;
        var usesTime = false;
        var usesHunt = false;
        if (row.ClearCount != 0)
        {
            earned = row.ClearCount <= clear.ClearCount;
        }
        else if (row.Time != 0 && row.HuntCount == 0)
        {
            usesTime = true;
            earned = clear.ElapsedSeconds < row.Time;
        }
        else if (row.Time == 0 && row.HuntCount != 0)
        {
            usesHunt = true;
            earned = row.HuntCount <= clear.HuntCount;
        }
        else if (row.Time != 0 && row.HuntCount != 0)
        {
            usesTime = true;
            usesHunt = true;
            earned = clear.ElapsedSeconds < row.Time && row.HuntCount <= clear.HuntCount;
        }
        else
        {
            earned = false;
        }
        if (!earned) return null;
        return new HonorTitleGrant(
            (ushort)row.Id,
            (uint)clear.QuestId,
            clear.QuestType,
            usesTime ? Clamp(clear.ElapsedSeconds) : (ushort)0,
            usesHunt ? Clamp(clear.HuntCount) : (ushort)0);
    }

    private static ushort Clamp(int value) => (ushort)Math.Clamp(value, 0, ushort.MaxValue);
}

public sealed class HonorTitleList
{
    private readonly List<ushort> _ids = new();
    private readonly object _gate = new();

    public HonorTitleList(IEnumerable<ushort> ids, Func<int, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(exists);
        foreach (var id in ids) TryAdd(id, exists);
    }

    public int Count
    {
        get { lock (_gate) return _ids.Count; }
    }

    public bool Contains(ushort titleId)
    {
        lock (_gate) return _ids.Contains(titleId);
    }

    public IReadOnlyList<ushort> Snapshot()
    {
        lock (_gate) return _ids.ToArray();
    }

    public bool TryAdd(ushort titleId, Func<int, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        lock (_gate)
        {
            if (!exists(titleId) || _ids.Contains(titleId) || _ids.Count >= HonorTitleRules.MaxOwned) return false;
            _ids.Add(titleId);
            return true;
        }
    }
}
