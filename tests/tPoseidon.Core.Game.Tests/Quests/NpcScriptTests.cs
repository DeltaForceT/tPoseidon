using tPoseidon.Core.Game.Quests;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class NpcScriptTests
{
    [Theory]
    [InlineData(1u, NpcScriptOperation.Exit, 0)]
    [InlineData(2u, NpcScriptOperation.QuestCheck, 0)]
    [InlineData(90u, NpcScriptOperation.QuestCheck, 1)]
    [InlineData(95u, NpcScriptOperation.QuestCheck, 6)]
    [InlineData(3u, NpcScriptOperation.QuestAccept, 0)]
    [InlineData(99u, NpcScriptOperation.QuestAccept, 4)]
    [InlineData(4u, NpcScriptOperation.QuestComplete, 0)]
    [InlineData(101u, NpcScriptOperation.QuestComplete, 2)]
    [InlineData(181u, NpcScriptOperation.QuestCompleteWithoutHistory, 0)]
    [InlineData(5u, NpcScriptOperation.QuestContinue, 0)]
    [InlineData(109u, NpcScriptOperation.QuestContinue, 4)]
    [InlineData(184u, NpcScriptOperation.QuestAcceptOncePerDay, 0)]
    [InlineData(203u, NpcScriptOperation.QuestAcceptOncePerDay, 2)]
    [InlineData(7u, NpcScriptOperation.MonsterQuestCheck, 0)]
    [InlineData(238u, NpcScriptOperation.MonsterQuestCheck, 1)]
    [InlineData(8u, NpcScriptOperation.MonsterQuestAccept, 0)]
    [InlineData(245u, NpcScriptOperation.MonsterQuestAccept, 4)]
    [InlineData(9u, NpcScriptOperation.MonsterQuestComplete, 0)]
    [InlineData(246u, NpcScriptOperation.MonsterQuestComplete, 1)]
    [InlineData(149u, NpcScriptOperation.MonsterQuestCompleteWithoutHistory, 0)]
    [InlineData(10u, NpcScriptOperation.MonsterQuestContinue, 0)]
    [InlineData(253u, NpcScriptOperation.MonsterQuestContinue, 4)]
    [InlineData(185u, NpcScriptOperation.MonsterQuestAcceptOncePerDay, 0)]
    [InlineData(83u, NpcScriptOperation.MonsterQuestState, 0)]
    [InlineData(122u, NpcScriptOperation.ScenarioQuestCheck, 0)]
    [InlineData(123u, NpcScriptOperation.ScenarioQuestCheck, 1)]
    [InlineData(129u, NpcScriptOperation.ScenarioQuestAccept, 2)]
    [InlineData(136u, NpcScriptOperation.ScenarioQuestComplete, 4)]
    [InlineData(137u, NpcScriptOperation.ScenarioQuestContinue, 0)]
    [InlineData(144u, NpcScriptOperation.ScenarioQuestActive, 2)]
    [InlineData(146u, NpcScriptOperation.ScenarioQuestState, 1)]
    [InlineData(82u, NpcScriptOperation.QuestState, 0)]
    [InlineData(105u, NpcScriptOperation.QuestState, 2)]
    [InlineData(115u, NpcScriptOperation.QuestActive, 1)]
    [InlineData(84u, NpcScriptOperation.HasItem, 0)]
    [InlineData(197u, NpcScriptOperation.ItemCount, 0)]
    [InlineData(198u, NpcScriptOperation.SecondItemCount, 0)]
    [InlineData(119u, NpcScriptOperation.GiveItem, 0)]
    [InlineData(120u, NpcScriptOperation.TakeItem, 0)]
    [InlineData(182u, NpcScriptOperation.TakeGelder, 0)]
    [InlineData(183u, NpcScriptOperation.GiveGelder, 0)]
    [InlineData(150u, NpcScriptOperation.OpenLookChange, 0)]
    [InlineData(201u, NpcScriptOperation.EnterFortuneHouse, 0)]
    [InlineData(222u, NpcScriptOperation.AwakenReset, 0)]
    [InlineData(65u, NpcScriptOperation.TakeIssuedItem, 0)]
    [InlineData(206u, NpcScriptOperation.OpenItemRecycle, 0)]
    [InlineData(207u, NpcScriptOperation.OpenMyCampRecycle, 0)]
    [InlineData(199u, NpcScriptOperation.OpenPetReinforce, 0)]
    [InlineData(200u, NpcScriptOperation.OpenPetReinforceReset, 0)]
    [InlineData(208u, NpcScriptOperation.OpenCouponExchange, 0)]
    [InlineData(267u, NpcScriptOperation.OpenSlotExtend, 0)]
    [InlineData(266u, NpcScriptOperation.OpenMelting, 0)]
    [InlineData(152u, NpcScriptOperation.OpenEquipComposing, 0)]
    [InlineData(176u, NpcScriptOperation.OpenEquipReform, 0)]
    [InlineData(209u, NpcScriptOperation.OpenEquipReform, 1)]
    [InlineData(177u, NpcScriptOperation.OpenPetRetrain, 0)]
    [InlineData(273u, NpcScriptOperation.OpenSoulWindow, 0)]
    [InlineData(274u, NpcScriptOperation.OpenSoulWindow, 1)]
    [InlineData(275u, NpcScriptOperation.OpenSoulWindow, 2)]
    [InlineData(268u, NpcScriptOperation.OpenGhostExchange, 0)]
    [InlineData(269u, NpcScriptOperation.OpenGhostExchange, 1)]
    public void FunctionIdsMapToTheServersHandlersAndSlots(uint functionId, NpcScriptOperation operation, int slot)
    {
        Assert.Equal(new NpcScriptCall(operation, slot), NpcScript.Classify(functionId));
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(24u)]
    [InlineData(61u)]
    [InlineData(213u)]
    [InlineData(261u)]
    [InlineData(0x0000047Au)]
    public void FunctionsWithoutAQuestHandlerAreUnhandled(uint functionId)
    {
        Assert.Equal(NpcScriptOperation.Unhandled, NpcScript.Classify(functionId).Operation);
    }

    [Fact]
    public void AResultPicksThatArgumentAsTheMessage()
    {
        var args = new ushort[] { 7, 8, 0x100, 9 };

        Assert.Equal(8, NpcScript.MessageFor(2, args));
        Assert.Equal(0x100, NpcScript.MessageFor(3, args));
    }

    [Fact]
    public void AResultBeyondTheArgumentsIsRefused()
    {
        Assert.Equal(NpcScript.Refused, NpcScript.MessageFor(5, new ushort[] { 1, 2 }));
    }

    [Theory]
    [InlineData(NpcScript.Refused)]
    [InlineData(NpcScript.Pending)]
    [InlineData(NpcScript.Silent)]
    public void ResultsBelowOneCarryNoMessage(int result)
    {
        Assert.Null(NpcScript.MessageFor(result, new ushort[] { 1 }));
    }

    [Fact]
    public void FunctionSeventeenOpensTheBank()
    {
        Assert.Equal(NpcScriptOperation.OpenBank, NpcScript.Classify(17).Operation);
    }

    [Fact]
    public void FunctionSixteenOpensTheWarehouse()
    {
        Assert.Equal(NpcScriptOperation.OpenWarehouse, NpcScript.Classify(16).Operation);
    }

    [Fact]
    public void FunctionThirtyFiveOpensTheCashItemBox()
    {
        Assert.Equal(NpcScriptOperation.OpenCashBox, NpcScript.Classify(35).Operation);
    }

    [Theory]
    [InlineData(74u, 0)]
    [InlineData(75u, 1)]
    [InlineData(76u, 2)]
    [InlineData(77u, 3)]
    public void TheFTypeChecksCarryTheirTypeIndex(uint functionId, int typeIndex)
    {
        Assert.Equal(new NpcScriptCall(NpcScriptOperation.FTypeOnly, typeIndex), NpcScript.Classify(functionId));
    }

    [Fact]
    public void RandomMessageAndGenderAreClassified()
    {
        Assert.Equal(NpcScriptOperation.RandomMessage, NpcScript.Classify(151).Operation);
        Assert.Equal(NpcScriptOperation.CheckGender, NpcScript.Classify(86).Operation);
    }

    [Theory]
    [InlineData(2, 2, 1)]
    [InlineData(1, 2, 2)]
    [InlineData(24, 2, 1)]
    [InlineData(25, 2, -1)]
    [InlineData(0, 2, -1)]
    [InlineData(2, 1, -1)]
    public void GenderIsEvenOrOddCharacterType(int characterType, int argCount, int result)
    {
        Assert.Equal(result, NpcScript.GenderResult(characterType, argCount));
    }

    [Fact]
    public void FTypeMatchesAnswerOneElseTwo()
    {
        Assert.Equal(1, NpcScript.FTypeResult(2, 2));
        Assert.Equal(2, NpcScript.FTypeResult(1, 2));
    }
}
