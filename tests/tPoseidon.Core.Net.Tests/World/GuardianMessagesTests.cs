using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GuardianMessagesTests
{
    private static readonly GuardianState Equipped = new(
        77, 0, 15000, "Spirit", 13, 1, 2, 3, 4, 3, 3, 3,
        [18, 0, 10, 80, 0, 0, 1200, 0, 0, 120, 0, -5], 500, 7, 10,
        [new GuardianSkill(2033, GuardianSkillType.Card, 1, 4, 3), new GuardianSkill(9000, GuardianSkillType.Special, 0, 1, 0)]);

    [Fact]
    public void LoginRoundTripsWithListAndEquippedGuardian()
    {
        var login = new GuardianLogin(true, 4, [new GuardianSummary(77, 15000, 0, true, "Spirit", 2), new GuardianSummary(78, 15004, 1, false, "Second", 0)], Equipped);

        var decoded = GuardianMessages.DecodeLogin(GuardianMessages.EncodeLogin(login));

        Assert.True(decoded.Awakened);
        Assert.Equal(4, decoded.SlotMaxCount);
        Assert.Equal(login.Guardians, decoded.Guardians);
        Assert.Equal(Equipped with { StatPoints = [], Skills = [] }, decoded.Equipped! with { StatPoints = [], Skills = [] });
        Assert.Equal(Equipped.StatPoints, decoded.Equipped!.StatPoints);
        Assert.Equal(Equipped.Skills, decoded.Equipped.Skills);
    }

    [Fact]
    public void LoginWithoutEquippedGuardianRoundTrips()
    {
        var decoded = GuardianMessages.DecodeLogin(GuardianMessages.EncodeLogin(new GuardianLogin(false, 0, [], null)));

        Assert.False(decoded.Awakened);
        Assert.Empty(decoded.Guardians);
        Assert.Null(decoded.Equipped);
    }

    [Fact]
    public void UpdatesRoundTrip()
    {
        Assert.Equal(new GuardianOrderChange(77, 2033, 5, true), GuardianMessages.DecodeOrder(GuardianMessages.EncodeOrder(new GuardianOrderChange(77, 2033, 5, true))));
        Assert.Equal(new GuardianStateUpdate(77, 500, 7), GuardianMessages.DecodeStateUpdate(GuardianMessages.EncodeStateUpdate(new GuardianStateUpdate(77, 500, 7))));
        var status = GuardianMessages.DecodeStatus(GuardianMessages.EncodeStatus(new GuardianStatusUpdate(77, 14, 2, Equipped.StatPoints)));
        Assert.Equal((77UL, (short)14, 2), (status.GuardianUid, status.Level, status.SkillPoint));
        Assert.Equal(Equipped.StatPoints, status.StatPoints);
        Assert.Equal(77UL, GuardianMessages.DecodeUid(GuardianMessages.EncodeUid(77)));
    }

    [Fact]
    public void RenameCarriesTheGuardianTheNameAndTheItemRow()
    {
        var row = new EquipUpgradeItem(EquipUpgradeAction.Update, 0x55, 19900, 1, 2, 0, 0, 0, 0);
        var rename = GuardianMessages.DecodeRename(GuardianMessages.EncodeRename(new GuardianRename(77, "Nova", new EquipStatusUpgrade(9, 0, 40, [row]))));
        Assert.Equal((77UL, "Nova", 9UL, 40), (rename.GuardianUid, rename.Name, rename.Item.CharacterId, rename.Item.BagCapacity));
        Assert.Equal(row, Assert.Single(rename.Item.Items));
    }

    [Fact]
    public void CardSaveRoundTripsWithItsItemOperations()
    {
        var items = new EquipStatusUpgrade(9, 0, 80, [new EquipUpgradeItem(EquipUpgradeAction.Update, 0x10, 27001, 1, 2, 3, 0, 0, 0)]);
        var save = new GuardianCardSave(77, 6, 2, [new GuardianSkill(2033, GuardianSkillType.Card, 1, 4, 3), new GuardianSkill(2034, GuardianSkillType.Card, 0, 1, 0)], items);

        var decoded = GuardianMessages.DecodeCardSave(GuardianMessages.EncodeCardSave(save));

        Assert.Equal((77UL, 6, 2), (decoded.GuardianUid, decoded.SkillPoint, decoded.UpdateCount));
        Assert.Equal(save.Cards, decoded.Cards);
        Assert.Equal(items.CharacterId, decoded.Items.CharacterId);
        Assert.Equal(items.Items, decoded.Items.Items);
    }
}
