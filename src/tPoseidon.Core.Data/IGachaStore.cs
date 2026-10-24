namespace tPoseidon.Core.Data;

public sealed record GachaPrize(int ItemIndex, byte ItemClass, short Count, short UseTimeHours, byte ItemFlag, int Weight);

public sealed record GachaDrawRequest(uint AccountId, short Area, bool Deep, string RemoteAddress);

public enum GachaDrawOutcome
{
    Won,
    NotEnoughCoupons,
    EmptyPool,
}

public sealed record GachaDrawResult(GachaDrawOutcome Outcome, GachaPrize? Prize, ulong ChargedItemUid, int CouponsLeft);

public enum GachaCouponSource : short
{
    Purchase = 0,
    Grant = 1,
}

public static class GachaRules
{
    public const short FirstArea = 61;
    public const short LastArea = 64;
    public const int NormalDrillCoupons = 1;
    public const int DeepDrillCoupons = 2;

    public static bool IsArea(int area) => area is >= FirstArea and <= LastArea;

    public static int CouponsFor(bool deep) => deep ? DeepDrillCoupons : NormalDrillCoupons;
}

public interface IGachaStore
{
    int CouponCount(uint accountId);

    void GrantCoupons(uint accountId, int count, GachaCouponSource source);

    IReadOnlyList<GachaPrize> Pool(short area, bool deep);

    GachaDrawResult Draw(GachaDrawRequest request, Func<IReadOnlyList<GachaPrize>, GachaPrize> pick);
}
