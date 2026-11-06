namespace tPoseidon.Core.LibConfig;

public sealed class NpcRowBinder
{
    private readonly INpcTalkLookup _lookup;
    private readonly Dictionary<(string Map, int PosId, int Type), int> _taken = new();

    public NpcRowBinder(INpcTalkLookup lookup)
    {
        _lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
    }

    public NpcTalkEntry? Next(string mapName, int posId, int type)
    {
        ArgumentNullException.ThrowIfNull(mapName);
        var rows = _lookup.GetAllByMap(mapName).Where(row => row.PosId == posId && row.Type == type).ToArray();
        if (rows.Length == 0) return _lookup.GetByPosOnMap(posId, mapName);
        _taken.TryGetValue((mapName, posId, type), out var taken);
        _taken[(mapName, posId, type)] = taken + 1;
        return rows[Math.Min(taken, rows.Length - 1)];
    }
}
