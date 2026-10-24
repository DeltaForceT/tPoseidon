using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Wedding;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class CoupleServiceTests
{
    private const ulong Groom = 0x2001;
    private const ulong Bride = 0x2002;

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeCouples : ICoupleRulesStore
    {
        public CoupleRecord? Couple { get; set; }

        public (byte State, IReadOnlyList<int> Quests, IReadOnlyList<int> Skills)? Deleted { get; private set; }

        public CoupleRegistration Register(ulong maleCharacterId, ulong femaleCharacterId, DateTimeOffset at)
        {
            if (Couple is not null) return new CoupleRegistration(CoupleStoreError.AlreadyCoupled, 0);
            Couple = Record(CoupleStates.Joined);
            return new CoupleRegistration(CoupleStoreError.None, Couple.CoupleCode);
        }

        public CoupleCharInfo? CharInfo(ulong characterId) => null;

        public CoupleRecord? Load(ulong coupleCode) => Couple?.CoupleCode == coupleCode ? Couple : null;

        public CoupleRecord? LoadForCharacter(ulong characterId) =>
            Couple?.Members.Any(member => member.CharacterId == characterId) == true ? Couple : null;

        public bool RequestDivorce(ulong coupleCode, ulong requesterId, ulong receiverId, DateTimeOffset at) => Set(CoupleStates.DivorceRequested);

        public bool CancelDivorce(ulong coupleCode) => Set(CoupleStates.Married);

        public bool ConfirmDivorce(ulong coupleCode, DateTimeOffset at) => Set(CoupleStates.Divorced);

        public bool Delete(ulong coupleCode, byte state, IReadOnlyList<int> questIds, IReadOnlyList<int> skillIds)
        {
            Deleted = (state, questIds, skillIds);
            Couple = null;
            return true;
        }

        public bool SetMarriageOptions(ulong coupleCode, int ringItemIndex, int dressItemIndex, short titleIndex, DateTimeOffset at) => Set(CoupleStates.OptionsChosen);

        public bool CompleteWedding(ulong coupleCode, DateTimeOffset at, IReadOnlyList<ulong> guests) => Set(CoupleStates.Married);

        public void RecordCoupleSkill(ulong coupleCode, ulong characterId, bool female, int skillId, DateTimeOffset at)
        {
        }

        public bool UpdateEventPoints(ulong coupleCode, ulong characterId, byte relation, byte love, byte friendship, long eventMask, byte state, DateTimeOffset at)
        {
            if (Couple is null) return false;
            Couple = Couple with
            {
                RelationPoint = relation,
                LovePoint = love,
                FriendPoint = friendship,
                State = state,
                RecentUpdate = at,
                Members = Couple.Members.Select(member => member with { EventBitmask = eventMask }).ToArray(),
            };
            return true;
        }

        private bool Set(byte state)
        {
            if (Couple is null) return false;
            Couple = Couple with { State = state };
            return true;
        }
    }

    private static CoupleRecord Record(byte state) => new(
        7, state, 86, 60, 70, 1, 2, 39500, 39000, 12, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        [new CoupleMember(Groom, false, 0, 0, "Gus", 4), new CoupleMember(Bride, true, 0, 0, "Bea", 5)]);

    private static readonly Dictionary<int, CoupleEventPoints> Points = new()
    {
        [CoupleCloseness.CouplePartySmall] = new(8, 3, 5),
        [CoupleCloseness.PartyWithoutLover] = new(-2, -2, -1),
        [CoupleCloseness.CouplePartyLarge] = new(4, 1, 3),
        [CoupleCloseness.MemoToLover] = new(4, 5, 1),
        [CoupleCloseness.MemoToOthers] = new(-2, -1, 0),
        [CoupleCloseness.BothOnline] = new(5, 3, 3),
        [CoupleCloseness.WhisperToLover] = new(4, 4, 2),
        [CoupleCloseness.CouplePartyQuest] = new(5, 4, 1),
        [CoupleCloseness.LoverPosition] = new(2, 2, -2),
        [CoupleCloseness.Anniversary] = new(10, 5, -3),
        [CoupleCloseness.GiftFromLover] = new(10, 10, 3),
    };

    private static CoupleService Service(FakeCouples store) => new(store, [7100, 7101], [5020], Points, Text, new FixedTime());

    private static CoupleRecord Dating(byte relation) => Record(CoupleStates.Joined) with { RelationPoint = relation, LovePoint = 50, FriendPoint = 50 };

    [Fact]
    public void EventsCountOncePerDayAndReachState1()
    {
        var store = new FakeCouples { Couple = Dating(80) };
        var service = Service(store);
        Assert.NotNull(service.Memo(Groom, Bride));
        Assert.Equal((byte)84, store.Couple!.RelationPoint);
        Assert.Null(service.Memo(Bride, Groom));
        Assert.NotNull(service.Whisper(Bride, Groom));
        Assert.Equal((byte)88, store.Couple.RelationPoint);
        Assert.Equal(CoupleStates.CloseEnough, store.Couple.State);
        Assert.Null(service.Whisper(Groom, 0x9999));
        Assert.NotNull(service.Memo(Groom, 0x9999));
        Assert.Equal((byte)86, store.Couple.RelationPoint);
    }

    [Fact]
    public void YesterdaysMaskDoesNotBlockToday()
    {
        var store = new FakeCouples { Couple = Dating(10) with { RecentUpdate = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero) } };
        store.Couple = store.Couple with { Members = store.Couple.Members.Select(member => member with { EventBitmask = CoupleCloseness.MemoToLover }).ToArray() };
        var service = Service(store);
        Assert.NotNull(service.Memo(Groom, Bride));
        Assert.Equal((byte)14, store.Couple!.RelationPoint);
    }

    [Fact]
    public void PartyEventsAndMarriedCouplesStop()
    {
        var store = new FakeCouples { Couple = Dating(0) };
        var service = Service(store);
        Assert.Single(service.PartyChanged([Groom, Bride]));
        Assert.Equal((byte)8, store.Couple!.RelationPoint);
        Assert.Single(service.PartyChanged([Groom, 0x3000, 0x3001]));
        Assert.Equal((byte)6, store.Couple.RelationPoint);
        store.Couple = store.Couple with { RecentUpdate = DateTimeOffset.UnixEpoch };
        Assert.Single(service.PartyChanged([Groom, Bride, 0x3000]));
        Assert.Equal((byte)10, store.Couple.RelationPoint);
        store.Couple = Record(CoupleStates.Married);
        Assert.Null(service.Memo(Groom, Bride));
    }

    [Fact]
    public void DailyGainOfTheRepeatableSourcesIsTwentyEight()
    {
        var once = new[]
        {
            CoupleCloseness.CouplePartySmall, CoupleCloseness.BothOnline, CoupleCloseness.WhisperToLover,
            CoupleCloseness.MemoToLover, CoupleCloseness.CouplePartyQuest, CoupleCloseness.LoverPosition,
        };
        Assert.Equal(28, once.Sum(bit => Points[bit].Relation));
        Assert.True(CoupleCloseness.IsAnniversary(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 20, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(CoupleCloseness.IsAnniversary(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 2, 21, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void MoveToLoverAsksTheLoverServerUnlessOfflineOrABarredZone()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.Joined) };
        var service = Service(store);
        int? ZoneType(ushort zone) => zone == 30 ? (int)CoupleSocialFrames.MoveToLoverBarredZoneType : 1;
        service.OpenSession(Groom);
        Assert.Empty(service.MoveToLover(Groom, 10, ZoneType).Messages);

        store.Couple = Record(CoupleStates.Married);
        var offline = Assert.Single(service.MoveToLover(Groom, 10, ZoneType).Messages);
        Assert.Equal((CoupleSocialFrames.LoverOffline, 0), (offline.Opcode, offline.Payload.Length));

        service.OpenSession(Bride);
        var barred = Assert.Single(service.MoveToLover(Groom, 30, ZoneType).Messages);
        Assert.Equal(CoupleSocialFrames.LoverOffline, barred.Opcode);
        Assert.Equal(new byte[] { 7, 0, 0, 0 }, barred.Payload);

        var asked = service.MoveToLover(Groom, 10, ZoneType);
        Assert.Empty(asked.Messages);
        var order = Assert.Single(asked.Orders);
        Assert.Equal((Bride, CoupleSocialFrames.OrderLoverPosition), (order.CharacterId, order.OrderType));
        Assert.True(CoupleSocialFrames.TryReadLoverPositionOrder(order.Data, out var lover, out var requester));
        Assert.Equal((Bride, Groom), (lover, requester));

        Assert.True(CoupleSocialFrames.TryReadLoverSpot(CoupleSocialFrames.LoverSpot(Groom, 12, -3, 40), out var spotFor, out var spotZone, out var spotX, out var spotY));
        Assert.Equal((Groom, 12u, (short)-3, (short)40), (spotFor, spotZone, spotX, spotY));
        Assert.True(CoupleSocialFrames.TryReadMoveToLover(new byte[] { 10, 0, 0, 0, 0, 0 }, out var zone));
        Assert.Equal(10, zone);
        Assert.False(CoupleSocialFrames.TryReadMoveToLover(new byte[] { 10, 0 }, out _));
    }

    [Fact]
    public void SessionOpenPushesTheCoupleInfo()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.Married) };
        var service = Service(store);
        var messages = service.OpenSession(Groom);
        var info = Assert.Single(messages);
        Assert.Equal(CoupleSocialFrames.CoupleInfo, info.Opcode);
        var bytes = info.Payload;
        Assert.Equal(7UL, BitConverter.ToUInt64(bytes, 0));
        Assert.Equal(Bride, BitConverter.ToUInt64(bytes, 8));
        Assert.Equal((uint)CoupleStates.Married, BitConverter.ToUInt32(bytes, 16));
        Assert.Equal("Bea\0", Text.GetString(bytes, 20, 4));
        Assert.Equal(1U, BitConverter.ToUInt32(bytes, 24));
        Assert.Equal(5U, BitConverter.ToUInt32(bytes, 24 + 5 * 4));
        Assert.Equal(24 + 6 * 4 + 16 + 4, bytes.Length);
        Assert.Equal(39500, BitConverter.ToUInt16(bytes, bytes.Length - 4));
        Assert.Equal(12, bytes[^2]);
        Assert.Equal(1, bytes[^1]);
        Assert.Empty(service.OpenSession(Groom));
    }

    [Fact]
    public void LoverPositionNeedsTheLoverOnline()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.Married) };
        var service = Service(store);
        service.OpenSession(Groom);
        Assert.Equal(CoupleSocialFrames.LoverOffline, Assert.Single(service.LoverPosition(Groom, _ => 100)).Opcode);
        service.OpenSession(Bride);
        var reply = Assert.Single(service.LoverPosition(Groom, id => id == Bride ? (ushort)558 : null));
        Assert.Equal(CoupleSocialFrames.LoverPositionReply, reply.Opcode);
        Assert.Equal(new byte[] { 0x2E, 0x02, 0x2E, 0x02 }, reply.Payload);
    }

    [Fact]
    public void BreakUpDeletesWithTheConfiguredListsAndDivorceFollowsTheStates()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.Married) };
        var service = Service(store);
        service.OpenSession(Groom);
        service.OpenSession(Bride);
        Assert.Empty(service.BreakUp(Groom).Messages);
        var (requested, _) = service.RequestDivorce(Groom);
        Assert.Contains(requested, m => m.To == Bride && m.Opcode == CoupleSocialFrames.DivorceRequested);
        Assert.Equal(CoupleStates.DivorceRequested, store.Couple!.State);
        service.AnswerDivorce(Bride, accept: false);
        Assert.Equal(CoupleStates.Married, store.Couple!.State);
        service.RequestDivorce(Bride);
        var (confirmed, _) = service.AnswerDivorce(Groom, accept: true);
        Assert.Equal(2, confirmed.Count(m => m.Opcode == CoupleSocialFrames.DivorceDone));
        Assert.Equal(CoupleStates.Divorced, store.Couple!.State);
        var (broken, _) = service.BreakUp(Bride);
        Assert.Equal(2, broken.Count(m => m.Opcode == CoupleSocialFrames.BrokenUp));
        Assert.Equal(CoupleStates.DivorceDeleted, store.Deleted!.Value.State);
        Assert.Equal([7100, 7101], store.Deleted.Value.Quests);
    }

    [Fact]
    public void MakeCoupleNeedsBothSessions()
    {
        var store = new FakeCouples();
        var service = Service(store);
        service.OpenSession(Groom);
        Assert.Empty(service.MakeCouple(Groom, Bride).Messages);
        service.OpenSession(Bride);
        var (made, _) = service.MakeCouple(Groom, Bride);
        Assert.Equal(2, made.Count(m => m.Opcode == CoupleSocialFrames.CoupleInfo));
    }

    [Fact]
    public void GiftFromTheLoverAddsPointsEveryTime()
    {
        var store = new FakeCouples { Couple = Dating(10) };
        var service = Service(store);
        Assert.Null(service.GiftAccepted(Groom, "Someone"));
        Assert.NotNull(service.GiftAccepted(Groom, "Bea"));
        Assert.NotNull(service.GiftAccepted(Groom, "Bea"));
        Assert.Equal((byte)30, store.Couple!.RelationPoint);
        Assert.Equal((byte)70, store.Couple.LovePoint);
        Assert.Equal((byte)56, store.Couple.FriendPoint);
    }

    [Fact]
    public void HallListSkipsHallsWithoutTheirCouple()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.OptionsChosen) };
        var service = Service(store);
        var halls = new[]
        {
            new WeddingHallListing(7, Groom, "Hall", true, 0x01020304, true),
            new WeddingHallListing(99, Bride, "Gone", false, 0, false),
        };
        var bytes = Assert.Single(service.HallList(Groom, halls)).Payload;
        var expected = new List<byte> { 1, 0 };
        expected.AddRange(BitConverter.GetBytes(7UL));
        expected.AddRange(BitConverter.GetBytes(Groom));
        expected.AddRange(Text.GetBytes("Hall\0"));
        expected.Add(1);
        expected.AddRange(BitConverter.GetBytes(0x01020304U));
        expected.AddRange(BitConverter.GetBytes(4U));
        expected.AddRange(Text.GetBytes("Gus\0"));
        expected.AddRange(BitConverter.GetBytes(5U));
        expected.AddRange(Text.GetBytes("Bea\0"));
        expected.Add(1);
        Assert.Equal(expected.ToArray(), bytes);
    }

    [Fact]
    public void ArticleListNeedsTheCoupleSessionAndWritesTheEntryOrder()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.OptionsChosen) };
        var service = Service(store);
        var article = new WeddingArticle(7, 2, 0x0304, 5, 6, 7, 8, "C", "T", "M", "F");
        Assert.Null(service.ArticleList(Groom, () => [article]));
        service.OpenSession(Groom);
        var bytes = Assert.Single(service.ArticleList(Groom, () => [article])!).Payload;
        var expected = new List<byte> { 1, 0 };
        expected.AddRange(BitConverter.GetBytes(7UL));
        expected.AddRange(new byte[] { 2, 0x04, 0x03, 5, 6, 7, 8 });
        expected.AddRange(Text.GetBytes("C\0T\0M\0F\0"));
        Assert.Equal(expected.ToArray(), bytes);
    }

    private sealed class FakeBoard : IWeddingBoardStore
    {
        public HashSet<ulong> Couples { get; } = new();

        public IReadOnlyList<WeddingArticle> Articles() => Array.Empty<WeddingArticle>();

        public bool HasArticle(ulong coupleCode) => Couples.Contains(coupleCode);

        public bool DeleteArticle(ulong coupleCode) => Couples.Remove(coupleCode);

        public WeddingArticle? Saved { get; private set; }

        public WeddingArticleSave SaveArticle(WeddingArticle article, DateTimeOffset at)
        {
            Saved = article;
            return Couples.Add(article.CoupleCode) ? WeddingArticleSave.Created : WeddingArticleSave.Updated;
        }
    }

    private static byte[] ArticleBody(string message, string title)
    {
        var body = new List<byte> { 3 };
        body.AddRange(Text.GetBytes(message + "\0" + title + "\0"));
        body.AddRange(new byte[] { 11, 12 });
        return body.ToArray();
    }

    [Fact]
    public void ArticleWriteStoresTheCoupleRowAndAnswersCreatedThenUpdated()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.OptionsChosen) with { DressItemIndex = 0x0102 } };
        var service = Service(store);
        var board = new FakeBoard();
        service.OpenSession(Groom);
        var created = Assert.Single(service.WriteArticle(Groom, ArticleBody("Msg", "Title"), board, modify: false).Messages);
        Assert.Equal((ushort)0x2EA3, created.Opcode);
        var expected = new List<byte>();
        expected.AddRange(BitConverter.GetBytes(7UL));
        expected.AddRange(new byte[] { 3, 0x02, 0x01, 11, 12, 4, 5 });
        expected.AddRange(Text.GetBytes("Msg\0Title\0Gus\0Bea\0"));
        Assert.Equal(expected.ToArray(), created.Payload);
        Assert.Equal("Title", board.Saved!.Title);
        var updated = Assert.Single(service.WriteArticle(Groom, ArticleBody("Msg2", "Title"), board, modify: true).Messages);
        Assert.Equal((ushort)0x2EA5, updated.Opcode);
    }

    [Fact]
    public void ArticleWriteRefusesWrongStateMissingArticleAndLongText()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.Married) };
        var service = Service(store);
        var board = new FakeBoard();
        service.OpenSession(Groom);
        Assert.Equal(BitConverter.GetBytes(0x11U), Assert.Single(service.WriteArticle(Groom, ArticleBody("M", "T"), board, modify: false).Messages).Payload);
        Assert.Equal(BitConverter.GetBytes(0x0BU), Assert.Single(service.WriteArticle(Groom, ArticleBody("M", "T"), board, modify: true).Messages).Payload);
        store.Couple = Record(CoupleStates.OptionsChosen);
        var longTitle = new string('t', 0x41);
        Assert.Equal(BitConverter.GetBytes(0x16U), Assert.Single(service.WriteArticle(Groom, ArticleBody("M", longTitle), board, modify: false).Messages).Payload);
        var longMessage = new string('m', 0x9B);
        Assert.Equal(BitConverter.GetBytes(0x16U), Assert.Single(service.WriteArticle(Groom, ArticleBody(longMessage, "T"), board, modify: false).Messages).Payload);
        Assert.Null(board.Saved);
    }

    [Fact]
    public void ArticleDeleteRemovesTheRowAndAnswersWithTheCoupleCode()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.OptionsChosen) };
        var service = Service(store);
        var board = new FakeBoard();
        board.Couples.Add(7);
        Assert.Empty(service.DeleteArticle(Groom, board).Messages);
        service.OpenSession(Groom);
        var answer = Assert.Single(service.DeleteArticle(Groom, board).Messages);
        Assert.Equal((ushort)0x2EA7, answer.Opcode);
        Assert.Equal(Groom, answer.To);
        Assert.Equal(BitConverter.GetBytes(7UL), answer.Payload);
        Assert.Empty(board.Couples);
    }

    [Fact]
    public void ArticleDeleteRefusesWith7WithoutACoupleAndWithBWithoutAnArticle()
    {
        var store = new FakeCouples { Couple = Record(CoupleStates.OptionsChosen) };
        var service = Service(store);
        var board = new FakeBoard();
        service.OpenSession(Groom);
        var missing = Assert.Single(service.DeleteArticle(Groom, board).Messages);
        Assert.Equal((ushort)0x2E8D, missing.Opcode);
        Assert.Equal(BitConverter.GetBytes(0x0BU), missing.Payload);
        store.Couple = null;
        var single = Assert.Single(service.DeleteArticle(Groom, board).Messages);
        Assert.Equal(BitConverter.GetBytes(7U), single.Payload);
    }
}
