using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class NpcMoveAndCheckRulesTests
{
    private static readonly (int ItemId, int Count)[] OneTicket = { (11081, 1) };
    private static readonly (int SkillId, int Level)[] TwoSkills = { (2001, 3), (0, 0), (2002, 1) };
    private static readonly Dictionary<int, int> Known = new() { [2001] = 5, [2002] = 1 };

    private static readonly NpcMoveEntry Move = new()
    {
        Id = 4,
        Zone = 47,
        DestId = 3,
        Items = OneTicket,
        MinLevel = 5,
        MaxLevel = 400,
        FType = NpcMoveEntry.AnyFType,
        CharacterType = NpcMoveEntry.AnyCharacterType,
    };

    private static Item Ticket(ushort stack) => new()
    {
        ItemUid = 9, ItemIndex = 11081, ItemClass = 1, Stack = stack, CharacterId = 1, SlotKind = SlotKind.Inventory, SlotPosition = 0,
    };

    [Fact]
    public void TheChecksRunItemsLevelFTypeThenCharacterType()
    {
        var bag = new[] { Ticket(1) };

        Assert.Equal(NpcMoveRules.Passed, NpcMoveRules.Check(Move, bag, 20, 1, 3));
        Assert.Equal(NpcMoveRules.ItemsMissing, NpcMoveRules.Check(Move, System.Array.Empty<Item>(), 20, 1, 3));
        Assert.Equal(NpcMoveRules.LevelOutside, NpcMoveRules.Check(Move, bag, 4, 1, 3));
        Assert.Equal(NpcMoveRules.FTypeMismatch, NpcMoveRules.Check(Move with { FType = 2 }, bag, 20, 1, 3));
        Assert.Equal(NpcMoveRules.CharacterTypeMismatch, NpcMoveRules.Check(Move with { CharacterType = 5 }, bag, 20, 1, 3));
    }

    [Fact]
    public void ZeroLevelBoundsMeanAnyLevel()
    {
        Assert.Equal(NpcMoveRules.Passed, NpcMoveRules.Check(Move with { MinLevel = 0, MaxLevel = 0 }, new[] { Ticket(1) }, 1, 0, 1));
    }

    [Theory]
    [InlineData(1, 1, 0, NpcCheckRules.Allowed)]
    [InlineData(2, 1, 0, NpcCheckRules.NotAllowed)]
    [InlineData(17, 0, 1, NpcCheckRules.Allowed)]
    [InlineData(34, -1, -1, NpcCheckRules.NotAllowed)]
    [InlineData(0, -1, -1, NpcCheckRules.NotAllowed)]
    public void JobsUseTheFirstMaskUpToSixteenAndTheSecondAbove(int job, int flag1, int flag2, int expected)
    {
        Assert.Equal(expected, NpcCheckRules.Job(job, flag1, flag2));
    }

    [Fact]
    public void SkillChecksReportMissingThenLowLevel()
    {
        Assert.Equal(NpcCheckRules.Allowed, NpcCheckRules.AllSkills(TwoSkills, Known));
        Assert.Equal(NpcCheckRules.SkillLevelLow, NpcCheckRules.SkillSlot((2002, 2), Known));
        Assert.Equal(NpcCheckRules.SkillMissing, NpcCheckRules.SkillSlot((2003, 1), Known));
    }

    [Theory]
    [InlineData(14u, NpcScriptOperation.MoveCheck, 0)]
    [InlineData(15u, NpcScriptOperation.MoveComplete, 0)]
    [InlineData(118u, NpcScriptOperation.CheckJob, 0)]
    [InlineData(188u, NpcScriptOperation.SkillCheckSlot, 2)]
    [InlineData(196u, NpcScriptOperation.SkillCheckAll, 0)]
    [InlineData(211u, NpcScriptOperation.SkillCheckAll, 2)]
    [InlineData(121u, NpcScriptOperation.RandomMove, 0)]
    public void FunctionsAreClassified(uint functionId, NpcScriptOperation operation, int slot)
    {
        Assert.Equal(new NpcScriptCall(operation, slot), NpcScript.Classify(functionId));
    }
}
