using tPoseidon.Core.Game.Progression;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Progression;

public sealed class HonorTitlesTests
{
    private static readonly HonorTitleBook Book = new(new[]
    {
        new HonorTitleEntry(1, 7, QuestTypes.Normal, 4065, 1, 0, 0),
        new HonorTitleEntry(2, 7, QuestTypes.Normal, 4065, 3, 0, 0),
        new HonorTitleEntry(9, 7, QuestTypes.Monster, 1054, 0, 30, 105),
        new HonorTitleEntry(10, 7, QuestTypes.Monster, 1054, 0, 30, 0),
        new HonorTitleEntry(11, 7, QuestTypes.Monster, 1054, 0, 0, 50),
    });

    private static HonorTitleList Owned(params ushort[] ids) => new(ids, Book.Exists);

    [Fact]
    public void ClearCountGrantsWhenTheQuestWasClearedOftenEnough()
    {
        var grants = Book.Grants(QuestClear.Counted(QuestTypes.Normal, 4065, 1), Owned());
        Assert.Equal(new[] { new HonorTitleGrant(1, 4065, QuestTypes.Normal, 0, 0) }, grants);
        Assert.Equal(new ushort[] { 1, 2 }, Book.Grants(QuestClear.Counted(QuestTypes.Normal, 4065, 3), Owned()).Select(grant => grant.TitleId));
    }

    [Fact]
    public void OwnedTitlesAreNotGrantedAgain()
    {
        Assert.Equal(new ushort[] { 2 }, Book.Grants(QuestClear.Counted(QuestTypes.Normal, 4065, 5), Owned(1)).Select(grant => grant.TitleId));
    }

    [Fact]
    public void TimeAndHuntRowsCompareElapsedBelowTimeAndHuntAtLeast()
    {
        var fast = Book.Grants(QuestClear.Hunted(QuestTypes.Monster, 1054, 1, 29_999, 120), Owned());
        Assert.Equal(new[]
        {
            new HonorTitleGrant(9, 1054, QuestTypes.Monster, 29, 120),
            new HonorTitleGrant(10, 1054, QuestTypes.Monster, 29, 0),
            new HonorTitleGrant(11, 1054, QuestTypes.Monster, 0, 120),
        }, fast);
        var slow = Book.Grants(QuestClear.Hunted(QuestTypes.Monster, 1054, 1, 30_000, 104), Owned());
        Assert.Equal(new ushort[] { 11 }, slow.Select(grant => grant.TitleId));
    }

    [Fact]
    public void AnotherQuestTypeWithTheSameIdGrantsNothing()
    {
        Assert.Empty(Book.Grants(QuestClear.Uncounted(QuestTypes.Scenario, 4065), Owned()));
    }

    [Fact]
    public void TheListRefusesUnknownDuplicateAndTheSixtyFirstTitle()
    {
        var everything = new HonorTitleBook(Enumerable.Range(1, 70).Select(id => new HonorTitleEntry(id, 7, 1, id, 1, 0, 0)));
        var list = new HonorTitleList(Array.Empty<ushort>(), everything.Exists);
        Assert.False(list.TryAdd(71, everything.Exists));
        Assert.True(list.TryAdd(1, everything.Exists));
        Assert.False(list.TryAdd(1, everything.Exists));
        for (ushort id = 2; id <= 60; id++) Assert.True(list.TryAdd(id, everything.Exists));
        Assert.False(list.TryAdd(61, everything.Exists));
        Assert.Equal(HonorTitleRules.MaxOwned, list.Count);
    }
}
