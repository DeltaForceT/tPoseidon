using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class GroundItemFramesTests
{
    private const byte CommonClass = 1;
    private const byte RareClass = 2;

    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static FieldItem Item(byte itemClass = CommonClass, int count = 3, bool hidden = false) =>
        new(0x0401, 153, 136, 152, 0x1234, count, itemClass, false, 0x0201, 0x0301, Start) { IsHidden = hidden };

    [Fact]
    public void ADropNamesTheDropperTheItemTheSpotAndTheItemBody()
    {
        var payload = GroundItemFrames.Dropped(Item());

        Assert.Equal(GroundItemFrames.DroppedSize, payload.Length);
        Assert.Equal(
            new byte[]
            {
                0x01, 0x02,
                0x01, 0x04,
                0x88, 0x00, 0x98, 0x00,
                0x00, 0x00,
                0x00, 0x80, 0x00,
                0x01,
                0x34, 0x12, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x03, 0x00,
            },
            payload);
    }

    [Fact]
    public void AnItemOfAShadowMonsterIsMarkedHidden()
    {
        var payload = GroundItemFrames.Dropped(Item(hidden: true));

        Assert.Equal(new byte[] { 0x01, 0x00 }, payload[8..10]);
    }

    [Fact]
    public void ARareItemCarriesFourMoreBytes()
    {
        var payload = GroundItemFrames.Dropped(Item(RareClass, count: 1));

        Assert.Equal(GroundItemFrames.DroppedSize + GroundItemFrames.RareItemExtraBytes, payload.Length);
        Assert.Equal(RareClass, payload[13]);
        Assert.Equal(new byte[4], payload[28..]);
    }

    [Fact]
    public void AnItemAlreadyOnTheGroundIsTheSameWithoutTheDropper()
    {
        var payload = GroundItemFrames.AlreadyThere(Item());

        Assert.Equal(GroundItemFrames.AlreadyThereSize, payload.Length);
        Assert.Equal(GroundItemFrames.Dropped(Item())[2..], payload);
    }

    [Fact]
    public void RemovalIsTheItemAndAZero()
    {
        Assert.Equal(new byte[] { 0x01, 0x04, 0x00 }, GroundItemFrames.Removed(0x0401));
    }

    [Fact]
    public void ARemovalReadsBackItsEntity()
    {
        Assert.Equal((ushort)0x0401, GroundItemFrames.ReadRemoved(GroundItemFrames.Removed(0x0401)));
    }

    [Fact]
    public void ARemovalOfTheWrongSizeIsRefused()
    {
        Assert.Throws<InvalidPacketException>(() => GroundItemFrames.ReadRemoved(new byte[] { 0x01, 0x04 }));
    }

    [Fact]
    public void APickupNamesThePlayerTheGroundItemAndTheCountTaken()
    {
        var payload = GroundItemFrames.PickedUp(0x0301, Item(), 3);

        Assert.Equal(GroundItemFrames.PickedUpSize, payload.Length);
        Assert.Equal(
            new byte[]
            {
                0x01, 0x03,
                0x01, 0x04,
                0x01,
                0x34, 0x12, 0x00, 0x00,
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0x03, 0x00,
            },
            payload);
    }

    [Fact]
    public void ABagChangeCarriesTheBagItemAndItsCount()
    {
        var payload = GroundItemFrames.AddedToBag(PickupRules.ItemCreated, CommonClass, 0x1234, 0x0000000100000002, 3);

        Assert.Equal(GroundItemFrames.AddedToBagSize, payload.Length);
        Assert.Equal(
            new byte[]
            {
                0x04, 0x01,
                0x34, 0x12, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00,
                0x03, 0x00,
                0x00,
            },
            payload);
    }

    [Fact]
    public void AGrownStackIsReportedWithItsNewTotal()
    {
        var payload = GroundItemFrames.AddedToBag(PickupRules.StackGrew, CommonClass, 0x1234, 7, 5);

        Assert.Equal(PickupRules.StackGrew, payload[0]);
        Assert.Equal(new byte[] { 0x05, 0x00, 0x00 }, payload[14..]);
    }

    [Fact]
    public void ACreatedRareItemCarriesAnEmptyOptionList()
    {
        var created = GroundItemFrames.AddedToBag(PickupRules.ItemCreated, RareClass, 0x1234, 7, 1);

        Assert.Equal(GroundItemFrames.AddedToBagSize + GroundItemFrames.RareItemExtraBytes + 1, created.Length);
        Assert.Equal(new byte[] { 0x00, 0x00 }, created[^2..]);
    }

    [Fact]
    public void MoneyIsTheNewTotal()
    {
        Assert.Equal(new byte[] { 0x40, 0xE2, 0x01, 0x00 }, GroundItemFrames.Money(123456));
        Assert.Equal(new byte[] { 0x01, 0x00 }, GroundItemFrames.MoneyFull());
    }

    [Fact]
    public void ABagItemIsNamedOnTheWireAsTheLoginListNamesIt()
    {
        Assert.Equal(0x0000000100000002UL, BagItemIds.OnWire(2));
        Assert.Equal(2UL, BagItemIds.Stored(0x0000000100000002UL));
    }

    [Fact]
    public void AStopNamesThePlayerThePositionAndTheMoveType()
    {
        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x48, 0x01, 0xE8, 0x00, 0x02 },
            GroundItemFrames.Stopped(0x0301, 328, 232, 2));
    }

    [Fact]
    public void DropsOfAKillFollowTheOwnerRewardsInsideTheBlock()
    {
        const ushort blockId = 7;
        var strike = new PlayerStrike(
            AttackerEntityId: 0x0301,
            TargetEntityId: 0x0201,
            AttackerX: 328,
            AttackerY: 232,
            TargetX: 344,
            TargetY: 248,
            Damage: 68,
            MobHpAfter: 0,
            HitFlags: 0,
            HitKind: 0x80,
            Killed: true,
            NextSwingAtUtc: default,
            MonsterParamId: 2100,
            MobMaxHp: 100);
        var additions = new SwingAdditions(
            null,
            new[] { (Opcode.S2C_OP_011D_TBD, new byte[5]) },
            new[] { (Opcode.S2C_OP_0406_TBD, new byte[4], SwingAudience.Attacker) },
            new[]
            {
                (Opcode.S2C_MONEY_DROPPED, GroundItemFrames.Dropped(Item())),
                (Opcode.S2C_OP_000C_TBD, GroundItemFrames.Dropped(Item())),
            });

        var frames = MeleeSwingFrames.Encode(strike, blockId, additions);

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_001B_TBD, MeleeSwingFrames.NoBlock, SwingAudience.Everyone),
                (Opcode.S2C_OP_001F_TBD, blockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_002A_TBD, blockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_002B_TBD, blockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_011D_TBD, blockId, SwingAudience.Owner),
                (Opcode.S2C_MONEY_DROPPED, blockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_000C_TBD, blockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_011E_TBD, blockId, SwingAudience.Engaged),
                (Opcode.S2C_OP_0406_TBD, blockId, SwingAudience.Attacker),
                (Opcode.S2C_COMMAND_BLOCK_END, blockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_0020_TBD, MeleeSwingFrames.NoBlock, SwingAudience.Everyone),
            },
            frames.Select(f => (f.Opcode, f.BlockId, f.Audience)).ToArray());
    }
}
