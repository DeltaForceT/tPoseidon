using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Wedding;

public readonly record struct CouplePoints(int Relation, int Love, int Friend)
{
    public static CouplePoints Of(CoupleRecord couple)
    {
        ArgumentNullException.ThrowIfNull(couple);
        return new CouplePoints(couple.RelationPoint, couple.LovePoint, couple.FriendPoint);
    }
}

public readonly record struct MarriageOptionRequest(ulong CouponWireUid, int TitleIndex, int RingItemId, int DressItemId);

public enum MarriageOptionRefusal
{
    None,
    NotCloseEnough,
    TitleMissing,
    TitleOutOfRange,
    RingMissing,
    RingOutOfRange,
    RingCoupon,
    DressMissing,
    DressOutOfRange,
    DressCoupon,
    Money,
}

public sealed record MarriageOptionChoice(
    MarriageTitleEntry Title,
    WeddingRingEntry Ring,
    MarriageExchangeEntry RingRow,
    WeddingDressEntry? Dress,
    MarriageExchangeEntry? DressRow)
{
    public int Fee => DressRow is null ? 0 : RingRow.Fee + DressRow.Fee;

    public IEnumerable<(int ItemId, int Count)> Coupons()
    {
        if (RingRow.ReqId != 0) yield return (RingRow.ReqId, RingRow.ReqNum);
        if (DressRow is { ReqId: not 0 }) yield return (DressRow.ReqId, DressRow.ReqNum);
    }
}

public static class CoupleRules
{
    public const int MinLevel = 30;
    public const byte ProposerRole = 1;
    public const byte TargetRole = 2;
    public const byte AcceptedFlagForAcceptor = 0;
    public const byte AcceptedFlagForProposer = 1;
    public const uint EndRefused = 0;
    public const uint EndProposerMissing = 1;
    public const uint EndConditionFailed = 2;
    public const uint EndTargetMissing = 4;
    public const byte ProposerMark = 1;
    public const byte TargetMark = 0;

    public const int MakeCouple = 225;
    public const int BrokenCouple = 226;
    public const int CheckMarriageState = 228;
    public const int ExchangeMarriage = 229;
    public const int OpenMarriageRoom = 230;
    public const int OpenMarriageBoard = 231;
    public const int BrokenMarriage = 232;
    public const int CheckCoupleSkill = 234;
    public const int CheckMarriageOption = 235;

    public const int SkillAlreadyKnown = 2;

    public const uint InfoMissingCode = 1;
    public const uint PointsOutOfRangeCode = 2;
    public const uint CouponMissingCode = 3;
    public const uint NotEnoughMoneyCode = 4;
    public const uint WrongCoupleStateCode = 5;

    public static bool IsCoupleFunction(uint functionId) => functionId is >= MakeCouple and <= CheckMarriageOption
        and not 227 and not 233;

    public static bool IsFemale(Species species) => (byte)species % 2 == 1;

    public static byte StateOf(CoupleRecord? couple) => couple?.State ?? CoupleStates.NoCouple;

    public static bool CanMakeCouple(byte coupleState, int level) => coupleState == CoupleStates.NoCouple && level >= MinLevel;

    public static int MarriageStateResult(byte coupleState) => coupleState switch
    {
        CoupleStates.Joined or CoupleStates.CloseEnough or CoupleStates.OptionsChosen => 2,
        CoupleStates.Married => 1,
        CoupleStates.DivorceRequested or CoupleStates.DivorceCancelled => 4,
        CoupleStates.Divorced => 3,
        _ => 4,
    };

    public static int? MarriageOptionResult(byte coupleState) => coupleState switch
    {
        CoupleStates.Joined => 3,
        CoupleStates.CloseEnough => 1,
        CoupleStates.OptionsChosen => 2,
        _ => null,
    };

    public static IReadOnlyList<MarriageExchangeEntry> ExchangeList(CoupleTables tables, CouplePoints points, Func<int, int> heldCount)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(heldCount);
        return tables.Exchange
            .Where(row => row.Points.Contains(points.Relation, points.Love, points.Friend))
            .Where(row => row.ReqId == 0 || heldCount(row.ReqId) >= row.ReqNum)
            .ToList();
    }

    public static IReadOnlyList<MarriageTitleEntry> TitleList(CoupleTables tables, CouplePoints points)
    {
        ArgumentNullException.ThrowIfNull(tables);
        return tables.Titles.Where(row => row.Points.Contains(points.Relation, points.Love, points.Friend)).ToList();
    }

    public static uint RefusalCode(MarriageOptionRefusal refusal) => refusal switch
    {
        MarriageOptionRefusal.TitleMissing or MarriageOptionRefusal.RingMissing or MarriageOptionRefusal.DressMissing => InfoMissingCode,
        MarriageOptionRefusal.TitleOutOfRange or MarriageOptionRefusal.RingOutOfRange or MarriageOptionRefusal.DressOutOfRange => PointsOutOfRangeCode,
        MarriageOptionRefusal.RingCoupon or MarriageOptionRefusal.DressCoupon => CouponMissingCode,
        MarriageOptionRefusal.Money => NotEnoughMoneyCode,
        MarriageOptionRefusal.NotCloseEnough => WrongCoupleStateCode,
        _ => throw new ArgumentOutOfRangeException(nameof(refusal), refusal, "A granted choice has no refusal code."),
    };

    public static (MarriageOptionRefusal Refusal, MarriageOptionChoice? Choice) Choose(
        CoupleTables tables, byte coupleState, CouplePoints points, MarriageOptionRequest request, Func<int, int> heldCount, long money)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(heldCount);
        if (coupleState != CoupleStates.CloseEnough) return (MarriageOptionRefusal.NotCloseEnough, null);
        var title = tables.Titles.FirstOrDefault(row => row.Index == request.TitleIndex);
        if (title is null) return (MarriageOptionRefusal.TitleMissing, null);
        if (!title.Points.Contains(points.Relation, points.Love, points.Friend)) return (MarriageOptionRefusal.TitleOutOfRange, null);
        var ringRow = tables.ExchangeRow(request.RingItemId);
        if (request.RingItemId == 0 || !tables.Rings.TryGetValue(request.RingItemId, out var ring) || ringRow is null) return (MarriageOptionRefusal.RingMissing, null);
        if (!ringRow.Points.Contains(points.Relation, points.Love, points.Friend)) return (MarriageOptionRefusal.RingOutOfRange, null);
        if (ringRow.ReqId != 0 && heldCount(ringRow.ReqId) < ringRow.ReqNum) return (MarriageOptionRefusal.RingCoupon, null);
        WeddingDressEntry? dress = null;
        MarriageExchangeEntry? dressRow = null;
        if (request.DressItemId != 0)
        {
            dressRow = tables.ExchangeRow(request.DressItemId);
            if (!tables.Dresses.TryGetValue(request.DressItemId, out dress) || dressRow is null) return (MarriageOptionRefusal.DressMissing, null);
            if (!dressRow.Points.Contains(points.Relation, points.Love, points.Friend)) return (MarriageOptionRefusal.DressOutOfRange, null);
            if (dressRow.ReqId != 0 && heldCount(dressRow.ReqId) < dressRow.ReqNum) return (MarriageOptionRefusal.DressCoupon, null);
        }
        var choice = new MarriageOptionChoice(title, ring, ringRow, dress, dressRow);
        if (money < choice.Fee) return (MarriageOptionRefusal.Money, null);
        return (MarriageOptionRefusal.None, choice);
    }

    public static int? CoupleSkillOf(CoupleTables tables, int ringItemIndex, bool female)
    {
        ArgumentNullException.ThrowIfNull(tables);
        if (!tables.Rings.TryGetValue(ringItemIndex, out var ring)) return null;
        var skill = female ? ring.SkillIdFemale : ring.SkillIdMale;
        return skill == 0 ? null : skill;
    }

    public static (ulong Male, ulong Female) Order(ulong firstId, bool firstFemale, ulong secondId, bool secondFemale) =>
        firstFemale && !secondFemale ? (secondId, firstId) : (firstId, secondId);
}
