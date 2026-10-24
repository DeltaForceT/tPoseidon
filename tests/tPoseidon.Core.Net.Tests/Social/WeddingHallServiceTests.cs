using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class WeddingHallServiceTests
{
    private const ulong Groom = 0x2001;
    private const ulong Bride = 0x2002;
    private const ulong Guest = 0x3001;
    private const ulong CoupleCode = 7;

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class Couples : ICoupleRulesStore
    {
        public CoupleRecord? Couple { get; set; } = Record(CoupleStates.OptionsChosen);

        public IReadOnlyList<ulong>? WeddingGuests { get; private set; }

        public CoupleRegistration Register(ulong maleCharacterId, ulong femaleCharacterId, DateTimeOffset at) => new(CoupleStoreError.StoreFailed, 0);

        public CoupleCharInfo? CharInfo(ulong characterId) => null;

        public CoupleRecord? Load(ulong coupleCode) => Couple?.CoupleCode == coupleCode ? Couple : null;

        public CoupleRecord? LoadForCharacter(ulong characterId) =>
            Couple?.Members.Any(member => member.CharacterId == characterId) == true ? Couple : null;

        public bool RequestDivorce(ulong coupleCode, ulong requesterId, ulong receiverId, DateTimeOffset at) => false;

        public bool CancelDivorce(ulong coupleCode) => false;

        public bool ConfirmDivorce(ulong coupleCode, DateTimeOffset at) => false;

        public bool Delete(ulong coupleCode, byte state, IReadOnlyList<int> questIds, IReadOnlyList<int> skillIds) => false;

        public bool SetMarriageOptions(ulong coupleCode, int ringItemIndex, int dressItemIndex, short titleIndex, DateTimeOffset at) => false;

        public bool CompleteWedding(ulong coupleCode, DateTimeOffset at, IReadOnlyList<ulong> guests)
        {
            WeddingGuests = guests;
            Couple = Couple! with { State = CoupleStates.Married };
            return true;
        }

        public void RecordCoupleSkill(ulong coupleCode, ulong characterId, bool female, int skillId, DateTimeOffset at)
        {
        }

        public bool UpdateEventPoints(ulong coupleCode, ulong characterId, byte relation, byte love, byte friendship, long eventMask, byte state, DateTimeOffset at) => false;
    }

    private static CoupleRecord Record(byte state) => new(
        CoupleCode, state, 86, 60, 70, 1, 2, 39500, 39003, 12, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        [new CoupleMember(Groom, false, 0, 0, "Gus", 4), new CoupleMember(Bride, true, 0, 0, "Bea", 5)]);

    private static WeddingHallService Service(Couples store, bool test = false) =>
        new(store, _ => true, id => [new CoupleMessage(id, CoupleSocialFrames.CoupleInfo, [])], Text, TimeProvider.System, test);

    private static uint Code(CoupleMessage message) => BitConverter.ToUInt32(message.Payload);

    [Fact]
    public void MakeNeedsOptionsChosenAndRepliesWithTheCoupleCodeAndDressesTheMaster()
    {
        var store = new Couples { Couple = Record(CoupleStates.CloseEnough) };
        var refused = Service(store).Make(Groom, "Gus", "Our day", "pw", 0x22E);
        Assert.Equal(WeddingHallError.InvalidState, Code(Assert.Single(refused.Messages)));

        store.Couple = Record(CoupleStates.OptionsChosen);
        var service = Service(store);
        var made = service.Make(Groom, "Gus", "Our day", "pw", 0x22E);
        var reply = Assert.Single(made.Messages);
        Assert.Equal(WeddingHallFrames.HallMade, reply.Opcode);
        Assert.Equal(BitConverter.GetBytes(CoupleCode), reply.Payload);
        var dress = Assert.Single(made.Orders);
        Assert.Equal(WeddingHallFrames.OrderDress, dress.OrderType);
        Assert.Equal(Groom, dress.CharacterId);
        Assert.Equal(new WeddingDressOrder(Groom, true, 39003), WeddingHallFrames.ReadDress(dress.Data));
        var listing = Assert.Single(service.Listings());
        Assert.Equal(new WeddingHallListing(CoupleCode, Groom, "Our day", true, 0x22E, false), listing);
        Assert.Equal(WeddingHallError.HallExists, Code(Assert.Single(service.Make(Bride, "Bea", "x", "", 0x22E).Messages)));
    }

    [Fact]
    public void JoinChecksThePasswordAndBroadcastsToTheMembersBeforeAdding()
    {
        var service = Service(new Couples());
        service.Make(Groom, "Gus", "T", "pw", 0x22E);
        Assert.Equal(WeddingHallError.WrongPassword, Code(Assert.Single(service.Join(Guest, "Gil", CoupleCode, "no").Messages)));
        Assert.Equal(WeddingHallError.JoinHallMissing, Code(Assert.Single(service.Join(Guest, "Gil", 99, "pw").Messages)));

        var joined = service.Join(Guest, "Gil", CoupleCode, "pw");
        var notice = joined.Messages[0];
        Assert.Equal((Groom, WeddingHallFrames.MemberJoined), (notice.To, notice.Opcode));
        Assert.Equal([.. BitConverter.GetBytes(Guest), .. Text.GetBytes("Gil\0")], notice.Payload);
        var detail = joined.Messages[1];
        Assert.Equal((Guest, WeddingHallFrames.HallJoined), (detail.To, detail.Opcode));
        var expected = new List<byte>();
        expected.AddRange(BitConverter.GetBytes(CoupleCode));
        expected.AddRange(BitConverter.GetBytes(0x22EU));
        expected.AddRange(BitConverter.GetBytes(Groom));
        expected.AddRange(BitConverter.GetBytes(Bride));
        expected.AddRange([2, 0]);
        expected.AddRange(BitConverter.GetBytes(Groom));
        expected.AddRange(Text.GetBytes("Gus\0"));
        expected.AddRange(BitConverter.GetBytes(Guest));
        expected.AddRange(Text.GetBytes("Gil\0"));
        expected.Add(0);
        Assert.Equal(expected.ToArray(), detail.Payload);
        Assert.Empty(joined.Orders);
        Assert.Equal(WeddingHallError.AlreadyInHall, Code(Assert.Single(service.Join(Guest, "Gil", CoupleCode, "pw").Messages)));
    }

    [Fact]
    public void StartNeedsTheLoverThenOrdersTheMasterToTheHallMapAndTheEnterOrderFollows()
    {
        var service = Service(new Couples());
        service.Make(Groom, "Gus", "T", string.Empty, 0x22F);
        Assert.Equal(WeddingHallError.LoverAbsent, Code(Assert.Single(service.Start(Groom, CoupleCode).Messages)));
        service.Join(Bride, "Bea", CoupleCode, string.Empty);
        Assert.Equal(WeddingHallError.NotMaster, Code(Assert.Single(service.Start(Bride, CoupleCode).Messages)));

        service.EnteredZone(Groom, 0x10);
        var started = service.Start(Groom, CoupleCode);
        var warp = Assert.Single(started.Orders);
        Assert.Equal((Groom, WeddingHallFrames.OrderWarpMaster), (warp.CharacterId!.Value, warp.OrderType));
        Assert.Equal(BitConverter.GetBytes(0x22FU), warp.Data);
        Assert.Equal(WeddingHallError.Started, Code(Assert.Single(service.Join(Guest, "Gil", CoupleCode, string.Empty).Messages)));

        var entered = Assert.Single(service.EnteredZone(Groom, 0x22F).Orders);
        Assert.Equal(WeddingHallFrames.OrderJoinMember, entered.OrderType);
        var join = WeddingHallFrames.ReadJoinMember(entered.Data, Text)!;
        Assert.Equal((CoupleCode, Groom, 0x22FU, 0x10U, "T"), (join.Wdid, join.MasterId, join.MapId, join.PreviousZone, join.Title));
        Assert.Equal([Groom, Bride], join.Members);

        var report = new WeddingEnterReport(WeddingEnterResult.Entered, 0x22F, 0x10, string.Empty);
        var call = Assert.Single(service.EnterReported(Groom, report).Orders);
        Assert.Equal(((ushort?)WeddingHallFrames.WarpMemberZone, WeddingHallFrames.OrderWarpMember), (call.Zone, call.OrderType));
        Assert.Equal([Groom, Bride], WeddingHallFrames.ReadWarpMember(call.Data, Text)!.Members);
    }

    [Fact]
    public void AFailedEnterReportReopensTheHallAndTellsEveryMember()
    {
        var service = Service(new Couples());
        service.Make(Groom, "Gus", "T", string.Empty, 0x22E);
        service.Join(Bride, "Bea", CoupleCode, string.Empty);
        service.Start(Groom, CoupleCode);
        var failed = service.EnterReported(Groom, new WeddingEnterReport(WeddingEnterResult.RequesterGone, 0, 0, string.Empty));
        Assert.Equal(2, failed.Messages.Count);
        Assert.All(failed.Messages, message => Assert.Equal(WeddingHallError.EnterRequesterGone, Code(message)));
        Assert.Single(service.Start(Groom, CoupleCode).Orders);
    }

    [Fact]
    public void KickProtectsTheCoupleAndAPartnerLeavingClosesTheHall()
    {
        var service = Service(new Couples());
        service.Make(Groom, "Gus", "T", string.Empty, 0x22E);
        service.Join(Bride, "Bea", CoupleCode, string.Empty);
        service.Join(Guest, "Gil", CoupleCode, string.Empty);
        Assert.Equal(WeddingHallError.KickLover, Code(service.Kick(Groom, CoupleCode, [Bride]).Messages.Single()));
        Assert.Equal(WeddingHallError.KickMaster, Code(service.Kick(Groom, CoupleCode, [Groom]).Messages.Single()));
        var kicked = service.Kick(Groom, CoupleCode, [Guest]);
        Assert.Equal(3, kicked.Messages.Count(message => message.Opcode == WeddingHallFrames.MemberKicked));
        Assert.Contains(kicked.Messages, message => message.Opcode == WeddingHallFrames.Kicked && message.To == Groom);

        var left = service.Exit(Bride, CoupleCode);
        Assert.Equal(2, left.Messages.Count(message => message.Opcode == WeddingHallFrames.MemberExited));
        Assert.Equal(2, left.Orders.Count(order => order.OrderType == WeddingHallFrames.OrderDress && !WeddingHallFrames.ReadDress(order.Data)!.On));
        Assert.Empty(service.Listings());
    }

    [Fact]
    public void EndWeddingMarriesTheCoupleOnceAndRefreshesBothPartners()
    {
        var store = new Couples();
        var service = Service(store);
        service.Make(Groom, "Gus", "T", string.Empty, 0x22E);
        var ended = service.Ended(new WeddingEnded(CoupleCode, [Guest]));
        Assert.Equal([Guest], store.WeddingGuests);
        Assert.Equal(2, ended.Messages.Count(message => message.Opcode == CoupleSocialFrames.CoupleInfo));
        Assert.Empty(service.Listings());
        Assert.Empty(service.Ended(new WeddingEnded(CoupleCode, [Guest])).Messages);
    }

    [Fact]
    public void EnterReportKeepsItsFixedNoteField()
    {
        var bytes = WeddingHallFrames.EnterReport(new WeddingEnterReport(0, 0x22E, 0x10, "n"), Text);
        Assert.Equal(12 + WeddingHallFrames.ReportNoteBytes, bytes.Length);
        Assert.Equal(new WeddingEnterReport(0, 0x22E, 0x10, "n"), WeddingHallFrames.ReadEnterReport(bytes, Text));
    }
}
