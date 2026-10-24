using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.CardBattle;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class CardBattleServiceTests
{
    private sealed class Stats : ICardBattleStore
    {
        public List<(ulong, ulong, bool)> Recorded { get; } = new();

        public CardBattleStats Load(ulong characterId, ulong otherId) => new(1, 2, 3, 0, 0, 0, 0);

        public void Record(ulong characterId, ulong otherId, bool won) => Recorded.Add((characterId, otherId, won));
    }

    private static readonly CardBattlePlayer A = new(1, "alpha", 40);
    private static readonly CardBattlePlayer B = new(2, "beta", 41);

    private static BattleCard[] Deck(ulong first) =>
        Enumerable.Range(0, 5).Select(i => new BattleCard(first + (ulong)i, 500, (uint)i, (byte)(i + 1), 0, 0, 0)).ToArray();

    [Fact]
    public void TheChanceCardCanBePlayedOnceWhenBehindAndIsMarkedForTheWinner()
    {
        var service = new CardBattleService(new Stats(), name => name == "beta" ? B : null, (_, _) => true, _ => 0, Encoding.ASCII);
        service.Make(A, "beta");
        service.Answer(B, yes: true);
        service.SelectRule(2, 0);
        BattleCard[] chanceDeck = [.. Deck(10)[..4], new BattleCard(14, 500, 5, 5, 0, 0, 0)];
        service.SubmitDeck(1, chanceDeck);
        service.SubmitDeck(2, Deck(20));
        Assert.Equal(CardBattleFrames.ChanceRefused, Assert.Single(service.Chance(1).Messages).Opcode);
        service.DropCard(1, 0, chanceDeck[0]);
        service.DropCard(2, 3, Deck(20)[3]);
        var chance = service.Chance(1);
        Assert.Equal([CardBattleFrames.ChanceDropped, CardBattleFrames.OpponentChanceDropped], chance.Messages.Select(m => m.Opcode));
        Assert.Equal(new byte[] { 4 }, chance.Messages[0].Payload);
        Assert.Equal(CardBattleFrames.ChanceRefused, Assert.Single(service.Chance(1).Messages).Opcode);
        var resolved = service.DropCard(2, 4, Deck(20)[4]);
        Assert.Contains(resolved.Messages, m => m.Opcode == CardBattleFrames.RoundResult);
    }

    [Fact]
    public void FullMatchSendsStartRoundsAndRecordsTheResult()
    {
        var stats = new Stats();
        var service = new CardBattleService(stats, name => name == "beta" ? B : null, (_, _) => true, _ => 0, Encoding.ASCII);
        Assert.Equal(CardBattleFrames.Asked, Assert.Single(service.Make(A, "beta").Messages).Opcode);
        var accepted = service.Answer(B, yes: true);
        Assert.Equal([CardBattleFrames.Accepted, CardBattleFrames.MasterStart, CardBattleFrames.OpponentStart], accepted.Messages.Select(m => m.Opcode));
        Assert.Equal(CardBattleFrames.StatBlockSize + CardBattleFrames.PlayerRecordSize, accepted.Messages[1].Payload.Length);
        Assert.Equal(7 + CardBattleFrames.PlayerRecordSize * 2, accepted.Messages[2].Payload.Length);
        service.SelectRule(2, 0);
        Assert.Empty(service.SubmitDeck(1, Deck(10)).Messages);
        var cards = service.SubmitDeck(2, Deck(20));
        Assert.All(cards.Messages, m => Assert.Equal(1 + CardBattleFrames.SideBlockSize * 2, m.Payload.Length));
        CardBattleOutbox last = cards;
        for (byte slot = 0; slot < 4; slot++)
        {
            service.DropCard(1, slot, Deck(10)[slot]);
            last = service.DropCard(2, (byte)(4 - slot), Deck(20)[4 - slot]);
        }
        var round = last.Messages.Last();
        Assert.Equal(CardBattleFrames.RoundResult, round.Opcode);
        Assert.Equal(3 + CardBattleFrames.SideBlockSize * 2 + ScoreBlock.Size * 2, round.Payload.Length);
        Assert.Equal(2, stats.Recorded.Count);
        Assert.NotNull(last.Steal);
        Assert.Equal(2, last.Orders.Count);
        Assert.All(last.Orders, order => Assert.Equal(CardBattleFrames.OrderStatusUpdate, order.OrderType));
        var winnerStatus = last.Orders.Select(order => CardBattleFrames.ReadStatus(order.Data)!.Value).Single(status => status.Winner);
        Assert.Equal(last.Steal.ItemId, winnerStatus.WonCardItemId);
        Assert.All(last.Orders, order => Assert.Equal(CardBattleFrames.StatusSize, order.Data.Length));
        Assert.Empty(service.StatusReported(1, CardBattleFrames.StatusOk, CardBattleRewardBlock.None).Messages);
        var final = service.StatusReported(2, CardBattleFrames.StatusOk, CardBattleRewardBlock.None).Messages;
        Assert.Equal(2, final.Count(m => m.Opcode == CardBattleFrames.FinalResult));
        Assert.All(final, m => Assert.Equal(12 + CardBattleFrames.SideBlockSize * 2 + ScoreBlock.Size * 2 + CardBattleFrames.RewardBlockSize * 2 + CardBattleFrames.StatBlockSize * 2, m.Payload.Length));
        Assert.Equal(CardBattleFrames.ResultWaiting, Assert.Single(service.ResultOk(1).Messages).Opcode);
        Assert.Equal(2, service.ResultOk(2).Messages.Count(m => m.Opcode == CardBattleFrames.ResultClosed));
    }

    [Fact]
    public void CardsInTheSideBlockAreShifted()
    {
        var card = new BattleCard(7, 500, 3, 33, 2, 9, 1);
        Assert.Equal(new BattleCard(7, 1000, 12, 8, 32, 288, 1), card.Encoded());
    }

    [Fact]
    public void StatusReplyForAnUnknownBattleIsTheNotInBattleError()
    {
        var service = new CardBattleService(new Stats(), _ => null, (_, _) => true, _ => 0, Encoding.ASCII);
        var reply = Assert.Single(service.StatusReported(5, CardBattleFrames.StatusOk, CardBattleRewardBlock.None).Messages);
        Assert.Equal((CardBattleFrames.BattleError, CardBattleFrames.ErrorNotInBattle), (reply.Opcode, reply.Payload[0]));
    }

    [Fact]
    public void RefusalsAndLeaving()
    {
        var service = new CardBattleService(new Stats(), name => name == "beta" ? B : null, (_, _) => true, _ => 99, Encoding.ASCII);
        Assert.Equal(CardBattleFrames.TargetNotFound, service.Make(A, "nobody").Messages.Single().Payload[0]);
        Assert.Equal(CardBattleFrames.AlreadyInBattle, service.Make(A, "beta").Messages.Single().Payload[0]);
        Assert.Equal(CardBattleFrames.Asked, service.Ask(A, "beta").Messages.Single().Opcode);
        Assert.Equal(CardBattleFrames.Refused, service.Answer(B, yes: false).Messages.Single().Payload[0]);
        Assert.Equal(CardBattleFrames.NoAsk, service.Answer(B, yes: true).Messages.Single().Payload[0]);
        service.Ask(A, "beta");
        service.Answer(B, yes: true);
        Assert.Equal(CardBattleFrames.OpponentLeft, service.Leave(1, 3).Messages.Single().Opcode);
        Assert.Empty(service.Leave(2, 0).Messages);
    }
}
