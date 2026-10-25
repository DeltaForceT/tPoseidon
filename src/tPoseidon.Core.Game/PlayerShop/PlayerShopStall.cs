namespace tPoseidon.Core.Game.PlayerShop;

public readonly record struct PlayerShopSlot(ulong WireUid, uint ItemId, int UnitPrice, ushort Count);

public readonly record struct PlayerShopPurchase(ushort KeeperId, ulong WireUid, uint ItemId, ushort Count, uint UnitPrice);

public enum PlayerShopBuyRefusal
{
    InvalidItem,
    ItemInBusiness,
    KeeperBusy,
}

public sealed class PlayerShopStall
{
    private readonly object _gate = new();
    private readonly List<PlayerShopSlot> _slots = new();
    private readonly HashSet<ushort> _visitors = new();
    private readonly HashSet<ulong> _busy = new();
    private bool _locked;
    private int _visits;

    public PlayerShopStall(ushort keeperId)
    {
        KeeperId = keeperId;
    }

    public ushort KeeperId { get; }

    public byte ShopType { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public bool Registered { get; private set; }

    public IReadOnlyList<PlayerShopSlot> Slots
    {
        get
        {
            lock (_gate) return _slots.ToArray();
        }
    }

    public int VisitsSinceStart
    {
        get
        {
            lock (_gate) return _visits;
        }
    }

    public int VisitorCount
    {
        get
        {
            lock (_gate) return _visitors.Count;
        }
    }

    public IReadOnlyList<ushort> Visitors
    {
        get
        {
            lock (_gate) return _visitors.ToArray();
        }
    }

    public void Register(byte shopType, string title, IReadOnlyList<PlayerShopSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(slots);
        lock (_gate)
        {
            ShopType = shopType;
            Title = title;
            _slots.Clear();
            _slots.AddRange(slots);
            Registered = true;
        }
    }

    public bool AddVisitor(ushort visitorId)
    {
        lock (_gate)
        {
            if (_visitors.Count >= PlayerShopRules.MaxVisitors) return false;
            if (_visitors.Add(visitorId)) _visits++;
            return true;
        }
    }

    public void RestartCounters()
    {
        lock (_gate) _visits = 0;
    }

    public void RemoveVisitor(ushort visitorId)
    {
        lock (_gate) _visitors.Remove(visitorId);
    }

    public void PruneVisitors(Func<ushort, bool> stillVisiting)
    {
        ArgumentNullException.ThrowIfNull(stillVisiting);
        lock (_gate) _visitors.RemoveWhere(visitor => !stillVisiting(visitor));
    }

    public PlayerShopBuyRefusal? Reserve(PlayerShopPurchase purchase)
    {
        lock (_gate)
        {
            var index = _slots.FindIndex(slot => slot.WireUid == purchase.WireUid);
            if (index < 0) return PlayerShopBuyRefusal.InvalidItem;
            var slot = _slots[index];
            if (slot.ItemId != purchase.ItemId || slot.UnitPrice != purchase.UnitPrice || purchase.Count == 0 || purchase.Count > slot.Count)
            {
                return PlayerShopBuyRefusal.InvalidItem;
            }
            if (_busy.Contains(purchase.WireUid)) return PlayerShopBuyRefusal.ItemInBusiness;
            if (_locked) return PlayerShopBuyRefusal.KeeperBusy;
            _locked = true;
            _busy.Add(purchase.WireUid);
            return null;
        }
    }

    public void Release(PlayerShopPurchase purchase, bool sold)
    {
        lock (_gate)
        {
            _locked = false;
            _busy.Remove(purchase.WireUid);
            if (!sold) return;
            var index = _slots.FindIndex(slot => slot.WireUid == purchase.WireUid);
            if (index < 0) return;
            var left = _slots[index].Count - purchase.Count;
            if (left > 0) _slots[index] = _slots[index] with { Count = (ushort)left };
            else _slots.RemoveAt(index);
        }
    }
}

public static class PlayerShopRules
{
    public const int MaxSlots = 12;
    public const int MaxVisitors = 8;
    public const int MaxUnitPrice = 1_000_000_000;
    public const long MaxSettledMoney = 0x7FFFFFFF;
    public const byte WrongZone = 1;
    public const byte VisitRefused = 0;
    public const byte VisitorsFull = 4;
    public const byte BagFull = 5;
    public const byte MoneyOutOfRange = 6;
    public const ushort BuyerResultFailed = 3;
    public const ushort KeeperRefusesSkills = 0x13;

    private static readonly int[] ShopZoneTypes = [1, 2, 5];

    public static bool OpensInZone(int zoneType) => ShopZoneTypes.Contains(zoneType);

    public static bool PriceAllowed(int unitPrice) => unitPrice is >= 0 and <= MaxUnitPrice;

    public static bool SlotCountAllowed(int count) => count is >= 1 and <= MaxSlots;

    public static bool MoneySettles(long buyerMoney, long keeperMoney, uint unitPrice, ushort count)
    {
        var total = (long)unitPrice * count;
        return total < 0x80000000L && buyerMoney - total >= 0 && keeperMoney + total <= MaxSettledMoney;
    }
}
