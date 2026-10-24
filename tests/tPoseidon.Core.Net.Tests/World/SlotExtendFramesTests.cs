using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SlotExtendFramesTests
{
    private static readonly ulong[] OneSubItem = [0x1_0000_0033UL];
    private static readonly byte[] OnlyEquipment = [1, 0, 0, 0, 0, 0, 0, 0, 0];
    private static readonly byte[] MoneyAndCount = [0xD2, 0x04, 0, 0, 2, 0];
    private static readonly byte[] RareRecordTail = [1, 7, 0, 3];
    private static readonly byte[] RefusedFive = [5];

    [Fact]
    public void RequestIsCountEquipmentMaterialThenSubItems()
    {
        byte[] payload =
        [
            3,
            0x11, 0, 0, 0, 1, 0, 0, 0,
            0x22, 0, 0, 0, 1, 0, 0, 0,
            0x33, 0, 0, 0, 1, 0, 0, 0,
        ];
        Assert.True(SlotExtendFrames.TryReadRequest(payload, out var request));
        Assert.Equal(0x1_0000_0011UL, request.EquipmentWireUid);
        Assert.Equal(0x1_0000_0022UL, request.MaterialWireUid);
        Assert.Equal(OneSubItem, request.SubItemWireUids);
        Assert.False(SlotExtendFrames.TryReadRequest(payload.AsSpan(0, 24), out _));
        Assert.False(SlotExtendFrames.TryReadRequest(OnlyEquipment, out _));
    }

    [Fact]
    public void ResultIsMoneyThenOperationsAndCarriesTheRareRecordBytes()
    {
        var equipment = new BagOperation(QuestResultFrames.StackChanged, 2, 40001, 0x1_0000_0011, 1, 3, 1, 7, 0);
        var material = new BagOperation(QuestResultFrames.Removed, 1, 60766, 0x1_0000_0022, 0);
        var frame = SlotExtendFrames.Result(1234, new[] { equipment, material });
        Assert.Equal(MoneyAndCount, frame[..6]);
        Assert.Equal(QuestResultFrames.ItemOperation(equipment).Concat(QuestResultFrames.ItemOperation(material)), frame[6..]);
        var rare = QuestResultFrames.ItemOperation(equipment);
        Assert.Equal(RareRecordTail, rare[^4..]);
        Assert.Equal(equipment, QuestResultFrames.ReadItemOperation(rare));
        Assert.Equal(RefusedFive, SlotExtendFrames.Refused(5));
    }

    [Fact]
    public void UpgradeMessagesRoundTrip()
    {
        var rows = new[]
        {
            new EquipUpgradeItem(EquipUpgradeAction.Update, 0x11, 40001, 2, 1, 3, 1, 7, 0),
            new EquipUpgradeItem(EquipUpgradeAction.Delete, 0x22, 60766, 1, 1, 0, 0, 0, 0),
            new EquipUpgradeItem(EquipUpgradeAction.Insert, 0, 433065, 1, 1, 0, 0, 0, 0),
        };
        var request = EquipStatusUpgradeMessages.DecodeRequest(EquipStatusUpgradeMessages.EncodeRequest(new EquipStatusUpgrade(0x99, 60000, 50, rows)));
        Assert.Equal(0x99UL, request.CharacterId);
        Assert.Equal(60000u, request.Fee);
        Assert.Equal(50, request.BagCapacity);
        Assert.Equal(rows, request.Items);
        var result = EquipStatusUpgradeMessages.DecodeResult(EquipStatusUpgradeMessages.EncodeResult(new EquipStatusUpgradeResult(0, 777, rows)));
        Assert.True(result.Succeeded);
        Assert.Equal(777u, result.RemainMoney);
        Assert.Equal(rows, result.Items);
        Assert.Equal(EquipStatusUpgradeRules.BagFull, EquipStatusUpgradeMessages.DecodeError(EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.BagFull)));
    }

    [Fact]
    public void UpgradeRowsBecomeBagOperations()
    {
        var item = new Item { ItemUid = 0x11, ItemIndex = 40001, ItemClass = 2, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0, RefineType = 1, RefineLevel = 7 };
        Assert.Equal(new BagOperation(QuestResultFrames.StackChanged, 2, 40001, 0x1_0000_0011, 1, 4, 1, 7, 0),
            EquipUpgradeOperations.ToBag(EquipUpgradeOperations.Update(item, 1, 4)));
        Assert.Equal(new BagOperation(QuestResultFrames.Removed, 2, 40001, 0x1_0000_0011, 0, 0, 1, 7, 0),
            EquipUpgradeOperations.ToBag(EquipUpgradeOperations.Delete(item)));
        var stack = item with { ItemUid = 0x22, ItemIndex = 60766, ItemClass = 1, Stack = 3, RefineType = 0, RefineLevel = 0 };
        Assert.Equal(EquipUpgradeAction.Update, EquipUpgradeOperations.Take(stack, 1).Action);
        Assert.Equal(2, EquipUpgradeOperations.Take(stack, 1).Count);
        Assert.Equal(EquipUpgradeAction.Delete, EquipUpgradeOperations.Take(stack, 3).Action);
    }
}
