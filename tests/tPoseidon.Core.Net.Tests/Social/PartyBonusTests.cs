using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.Net.Social;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class PartyBonusTests
{
    private static readonly short[] NoPoints = new short[12];

    private static PartyService FourOfOneClassOnOneMap(ushort[] kinds)
    {
        var service = new PartyService(Encoding.Latin1, PartyRules.FromIni(1, 600_000), TimeProvider.System);
        for (var i = 0; i < kinds.Length; i++) service.Login((ulong)(0x1001 + i), $"P{i}", kinds[i], (ushort)(50 + i));
        service.Make(0x1001, "Crew", "P1");
        service.AcceptInvite(0x1002);
        for (var i = 2; i < kinds.Length; i++)
        {
            service.Invite(0x1001, $"P{i}");
            service.AcceptInvite((ulong)(0x1001 + i));
        }
        for (var i = 0; i < kinds.Length; i++) service.ZoneEntered((ulong)(0x1001 + i), 7);
        return service;
    }

    [Fact]
    public void AFourMemberTypePartyGetsTheMastersClassBonus()
    {
        var service = FourOfOneClassOnOneMap(new ushort[] { 3, 4, 12, 22 });

        var sent = service.ZoneEntered(0x1001, 7);

        var orders = sent.Orders.Where(o => o.OrderType == SystemLinkFrames.OrderTypeBonus && !o.IsCancel).ToArray();
        Assert.Equal(4, orders.Length);
        Assert.All(orders, o => Assert.Equal(PartyBonusBook.FirstClassBonus + 1, BitConverter.ToUInt32(o.Data!, 0)));
    }

    [Fact]
    public void ATwoMemberPartyHasNoTypeBonus()
    {
        var service = FourOfOneClassOnOneMap(new ushort[] { 3, 4 });

        Assert.DoesNotContain(service.ZoneEntered(0x1001, 7).Orders, o => o.OrderType == SystemLinkFrames.OrderTypeBonus && !o.IsCancel);
    }

    [Fact]
    public async Task AClassBonusAddsFourPointsToThatClassesThreeStats()
    {
        var link = new SystemServerLink(_ => true, _ => { });
        var book = new PartyBonusBook(link);
        await book.ApplyAsync(new OfficerOrder(1, 1, SystemLinkFrames.OrderTypeBonus, 0xAA, BitConverter.GetBytes(PartyBonusBook.FirstClassBonus + 2)), CancellationToken.None);

        var points = book.WithBonus(0xAA, NoPoints);

        Assert.Equal(new short[] { 0, 0, 0, 0, 0, 0, 4, 4, 4, 0, 0, 0 }, points);
        Assert.Equal(NaturalRecovery.PeriodMs, book.RecoveryPeriod(0xAA, NaturalRecovery.PeriodMs));
    }

    [Fact]
    public async Task ASameGenderBonusShortensRecoveryAndACancelRemovesIt()
    {
        var link = new SystemServerLink(_ => true, _ => { });
        var book = new PartyBonusBook(link);
        await book.ApplyAsync(new OfficerOrder(1, 9, SystemLinkFrames.OrderTypeBonus, 0xAA, BitConverter.GetBytes(PartyBonusBook.SameGenderBonus)), CancellationToken.None);

        Assert.Equal(1600, book.RecoveryPeriod(0xAA, NaturalRecovery.PeriodMs));
        await book.ApplyAsync(new OfficerOrder(1, 10, SystemLinkFrames.OrderCancel, 0xAA, SystemLinkFrames.CancelData(9)), CancellationToken.None);
        Assert.Null(book.BonusOf(0xAA));
    }

    [Fact]
    public void RecoveryTicksOnTheConfiguredPeriod()
    {
        var recovery = new NaturalRecovery { Period = 1600 };
        var amounts = new RecoveryAmounts(5, 5);

        recovery.Tick(NaturalRecovery.PeriodMs, 10, 100, 10, 100, amounts);
        Assert.Null(recovery.Tick(1500, 15, 100, 15, 100, amounts).Hp);
        Assert.Equal(20, recovery.Tick(100, 15, 100, 15, 100, amounts).Hp);
    }

    [Fact]
    public void TheHistoryListWritesShortRowsAndLongMonsterRows()
    {
        var normal = new QuestHistory { CharacterId = 1, QuestType = 1, QuestId = 1234, CompletionCount = 2 };
        var monster = new QuestHistory { CharacterId = 1, QuestType = 2, QuestId = 50, CompletionCount = 1, MonsterId = 2094, KillCount = 30, RecordTime = 600 };

        Assert.Equal(new byte[] { 1, 1, 0, 0xD2, 0x04, 2 }, QuestFrames.HistoryListFor(1, new[] { normal }));
        Assert.Equal(new byte[] { 2, 1, 0, 50, 0, 0x2E, 0x08, 1, 30, 0, 0x58, 0x02, 0, 0 }, QuestFrames.HistoryListFor(2, new[] { monster }));
    }
}
