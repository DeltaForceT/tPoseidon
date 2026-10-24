using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class SetItemBookTests
{
    private static readonly int[] TwoMembers = { 50000, 54000 };
    private static readonly int[] AllMembersAndMore = { 50000, 54000, 56000, 1 };
    private static readonly SetItemEntry Crystal = new(1, new[] { 50000, 54000, 56000 }, new[] { 400, 25, -1, 1400, 25, 400, 2000, 25, 25, 1400, 400, 25 }, string.Empty);

    [Fact]
    public void ASetIsActiveOnlyWhenEveryMemberIsWorn()
    {
        var book = new SetItemBook(new[] { Crystal });

        Assert.Empty(book.Active(TwoMembers));
        Assert.Equal(Crystal, Assert.Single(book.Active(AllMembersAndMore)));
    }

    [Fact]
    public void ActiveSetsAddTheirBonuses()
    {
        var bonus = SetItemBook.Bonus(new[] { Crystal, Crystal });

        Assert.Equal(800, bonus[0]);
        Assert.Equal(-2, bonus[2]);
        Assert.Equal(EquipmentRules.ItemStatCount, bonus.Length);
    }
}
