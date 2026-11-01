namespace tPoseidon.Core.Game.MyCamp;

public readonly record struct MyCampSitter(ushort PlayerId, byte State);

public sealed class MyCampSeating
{
    public const int MaxSeatsPerObject = 2;
    public const int MaxObjectsListed = 16;
    public const byte Standing = 0;
    public const byte Sitting = 2;

    private const int FlagSeatA = 0x02;
    private const int FlagSeatB = 0x04;
    private const int FlagSeatC = 0x08;
    private const int FlagSeatD = 0x10;

    private readonly Dictionary<ulong, MyCampSitter?[]> _objects = new();

    public static int SeatCount(int attr)
    {
        if ((attr & (FlagSeatA | FlagSeatB)) != 0)
            return ((attr & FlagSeatA) != 0 ? 1 : 0) + ((attr & FlagSeatB) != 0 ? 1 : 0);
        return ((attr & FlagSeatC) != 0 ? 1 : 0) + ((attr & FlagSeatD) != 0 ? 1 : 0);
    }

    public int Sit(ulong objectUid, int seatCount, ushort playerId, byte state)
    {
        var seats = Math.Min(seatCount, MaxSeatsPerObject);
        if (seats <= 0) return -1;
        if (!_objects.TryGetValue(objectUid, out var list))
        {
            list = new MyCampSitter?[MaxSeatsPerObject];
            _objects[objectUid] = list;
        }
        for (var seat = 0; seat < seats; seat++)
        {
            if (list[seat] is not null) continue;
            list[seat] = new MyCampSitter(playerId, state);
            return seat;
        }
        return -1;
    }

    public bool Stand(ulong objectUid, ushort playerId)
    {
        if (!_objects.TryGetValue(objectUid, out var list)) return false;
        for (var seat = 0; seat < list.Length; seat++)
        {
            if (list[seat] is not { } sitter || sitter.PlayerId != playerId) continue;
            list[seat] = null;
            if (list.All(entry => entry is null)) _objects.Remove(objectUid);
            return true;
        }
        return false;
    }

    public void Release(ushort playerId)
    {
        foreach (var uid in _objects.Keys.ToArray()) Stand(uid, playerId);
    }

    public void Clear() => _objects.Clear();

    public IReadOnlyList<(ulong ObjectUid, IReadOnlyList<MyCampSitter> Sitters)> Occupied() =>
        _objects
            .OrderBy(entry => entry.Key)
            .Select(entry => (entry.Key, (IReadOnlyList<MyCampSitter>)entry.Value.OfType<MyCampSitter>().ToArray()))
            .Where(entry => entry.Item2.Count > 0)
            .Take(MaxObjectsListed)
            .ToArray();
}
