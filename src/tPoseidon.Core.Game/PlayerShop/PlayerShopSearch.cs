using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.PlayerShop;

public sealed record PlayerShopSearchQuery(byte Category, byte Grade, ushort MinLevel, ushort MaxLevel, string Text);

public readonly record struct PlayerShopSearchRow(
    ushort KeeperId,
    byte CharacterType,
    string KeeperName,
    byte ItemClass,
    uint ItemId,
    ulong WireUid,
    ushort Stack,
    uint UnitPrice,
    ushort Count);

public readonly record struct PlayerShopRemoteBuy(ushort KeeperId, ulong WireUid, ushort Count, uint TotalPrice);

public sealed class PlayerShopSearchResult
{
    private readonly List<PlayerShopSearchRow> _rows;

    public PlayerShopSearchResult(IEnumerable<PlayerShopSearchRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        _rows = rows.ToList();
    }

    public int Count => _rows.Count;

    public int NextPage { get; private set; }

    public IReadOnlyList<PlayerShopSearchRow> TakePage()
    {
        var page = _rows.Skip(NextPage * PlayerShopSearchRules.PageSize).Take(PlayerShopSearchRules.PageSize).ToList();
        if (page.Count > 0) NextPage++;
        return page;
    }
}

public static class PlayerShopSearchRules
{
    public const int PageSize = 10;
    public const byte OpenOk = 0;
    public const byte OpenRefused = 1;

    public const byte InvalidZone = 1;
    public const byte InvalidBuyerState = 2;
    public const byte InvalidSeller = 3;
    public const byte InvalidSellerState = 4;
    public const byte InvalidSellItem = 5;
    public const byte BuyProcessingItem = 6;
    public const byte NotEnoughItemCount = 7;
    public const byte NotEnoughMoney = 8;
    public const byte OverflowWeight = 9;
    public const byte FullInventory = 10;
    public const byte EtcError = 11;
    public const byte PriceChanged = 13;

    public const byte WeaponCategory = 0;
    public const byte GunCategory = 1;
    public const byte HatCategory = 2;
    public const byte ShieldCategory = 3;
    public const byte CapeCategory = 4;
    public const byte AccessoryCategory = 5;
    public const byte ArmourCategory = 6;
    public const byte PetCategory = 7;
    public const byte HeadCategory = 8;
    public const byte FaceCategory = 9;
    public const byte DrillCategory = 10;
    public const byte FashionCategory = 11;
    public const byte PotionCategory = 12;
    public const byte CardCategory = 13;
    public const byte OtherCategory = 14;

    public const int WeaponType = 2;
    public const int HatType = 3;
    public const int BeltType = 4;
    public const int ShoesType = 5;
    public const int DrillType = 6;
    public const int AccessoryType = 7;
    public const int ShieldType = 8;
    public const int PotionType = 10;
    public const int CardType = 11;
    public const int PetType = 13;
    public const int HeadType = 24;
    public const int FaceType = 25;
    public const int CapeType = 34;
    public static readonly int[] FashionTypes = [17, 18, 19, 20, 21, 22, 23, 26, 27];

    public static byte CategoryOf(ItemParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Type switch
        {
            WeaponType => definition.SubType == ItemParamEntry.GunSubType ? GunCategory : WeaponCategory,
            HatType => HatCategory,
            ShieldType => ShieldCategory,
            CapeType => CapeCategory,
            AccessoryType => AccessoryCategory,
            BeltType or ShoesType => ArmourCategory,
            PetType => PetCategory,
            HeadType => HeadCategory,
            FaceType => FaceCategory,
            DrillType => DrillCategory,
            PotionType => PotionCategory,
            CardType => CardCategory,
            _ when FashionTypes.Contains(definition.Type) => FashionCategory,
            _ => OtherCategory,
        };
    }

    public static bool Matches(ItemParamEntry definition, PlayerShopSearchQuery query)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(query);
        if (CategoryOf(definition) != query.Category) return false;
        if (definition.MinLevel < query.MinLevel || definition.MinLevel > query.MaxLevel) return false;
        if (query.Text.Length == 0) return true;
        return definition.Name.Contains(query.Text, StringComparison.OrdinalIgnoreCase)
            || definition.NameEng.Contains(query.Text, StringComparison.OrdinalIgnoreCase);
    }

    public static bool PriceHolds(uint unitPrice, PlayerShopRemoteBuy buy) => (long)unitPrice * buy.Count == buy.TotalPrice;
}
