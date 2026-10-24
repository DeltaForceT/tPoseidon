using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class ImprintBookTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnImprintLastsItsHoursAndAddsToTheNamedStat()
    {
        var book = new ImprintBook();

        var added = book.Add(new ImprintEntry(37109, 24, 0, 1000f), Now);

        Assert.Equal(Now.AddHours(24), added.Until);
        Assert.Equal(1000, book.StatBonus(Now.AddHours(23))[0]);
        Assert.Equal(0, book.StatBonus(Now.AddHours(24))[0]);
    }

    [Fact]
    public void DrinkingTheSameTypeAgainReplacesTheOldImprint()
    {
        var book = new ImprintBook();
        book.Add(new ImprintEntry(37119, 24, 10, 1000f), Now);
        book.Add(new ImprintEntry(37133, 1, 10, 500f), Now);

        Assert.Equal(500, book.StatBonus(Now)[10]);
        Assert.Single(book.Active(Now));
    }

    [Fact]
    public void ExpAndTmImprintsArePercentBonuses()
    {
        var book = new ImprintBook();
        book.Add(new ImprintEntry(37129, 24, ImprintBook.ExpType, 200f), Now);
        book.Add(new ImprintEntry(37130, 24, ImprintBook.TmType, 50f), Now);

        Assert.Equal(300, ImprintBook.Scale(100, book.ExpPercent(Now)));
        Assert.Equal(150, ImprintBook.Scale(100, book.TmPercent(Now)));
        Assert.Equal(100, ImprintBook.Scale(100, book.ExpPercent(Now.AddHours(25))));
    }

    [Fact]
    public void ARestoredImprintKeepsItsOriginalEnd()
    {
        var book = new ImprintBook();

        book.Restore(new ActiveImprint(37109, 0, 1000f, Now.AddHours(2)));

        Assert.Equal(1000, book.StatBonus(Now)[0]);
        Assert.Equal(0, book.StatBonus(Now.AddHours(3))[0]);
    }

    [Fact]
    public void AnExpPotionScalesUntilItEnds()
    {
        var potion = new ExpPotion(1.5f, 1.8f, Now.AddMinutes(30));

        Assert.Equal(150, potion.ScaleExp(100, Now));
        Assert.Equal(180, potion.ScaleTm(100, Now));
        Assert.Equal(100, potion.ScaleExp(100, Now.AddMinutes(30)));
        Assert.Equal(0, potion.ScaleExp(0, Now));
    }
}
