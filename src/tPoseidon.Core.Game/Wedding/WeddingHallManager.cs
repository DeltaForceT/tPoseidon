using System.Collections.Concurrent;

namespace tPoseidon.Core.Game.Wedding;

public sealed class WeddingHallManager
{
    private readonly ConcurrentDictionary<ushort, WeddingHall> _byZone = new();

    public WeddingHall? Find(ushort zone) => _byZone.TryGetValue(zone, out var hall) ? hall : null;

    public bool TryOpen(WeddingHall hall)
    {
        ArgumentNullException.ThrowIfNull(hall);
        return _byZone.TryAdd(hall.Zone, hall);
    }

    public bool Close(WeddingHall hall)
    {
        ArgumentNullException.ThrowIfNull(hall);
        return _byZone.TryRemove(new KeyValuePair<ushort, WeddingHall>(hall.Zone, hall));
    }

    public IReadOnlyCollection<WeddingHall> Halls => _byZone.Values.ToArray();
}

public sealed class WeddingHall
{
    private readonly object _gate = new();
    private readonly HashSet<ulong> _guests = new();
    private readonly HashSet<ulong> _invited;

    public WeddingHall(
        ushort zone, ulong coupleCode, ulong masterId, WeddingPartner groom, WeddingPartner bride, IEnumerable<ulong> invited, WeddingCeremony ceremony)
    {
        ArgumentNullException.ThrowIfNull(invited);
        Zone = zone;
        CoupleCode = coupleCode;
        MasterId = masterId;
        Groom = groom ?? throw new ArgumentNullException(nameof(groom));
        Bride = bride ?? throw new ArgumentNullException(nameof(bride));
        Ceremony = ceremony ?? throw new ArgumentNullException(nameof(ceremony));
        _invited = new HashSet<ulong>(invited);
    }

    public ushort Zone { get; }
    public ulong CoupleCode { get; }
    public ulong MasterId { get; }
    public WeddingPartner Groom { get; }
    public WeddingPartner Bride { get; }
    public WeddingCeremony Ceremony { get; }

    public bool IsMember(ulong characterId) => characterId == Groom.CharacterId || characterId == Bride.CharacterId;

    public bool IsInvited(ulong characterId) => IsMember(characterId) || _invited.Contains(characterId);

    public bool Join(ulong characterId)
    {
        if (!IsInvited(characterId)) return false;
        if (IsMember(characterId)) return true;
        lock (_gate) _guests.Add(characterId);
        return true;
    }

    public IReadOnlyList<ulong> Guests
    {
        get
        {
            lock (_gate) return _guests.ToArray();
        }
    }
}

public sealed record WeddingPartner(ulong CharacterId, string Name);
