using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Quests;

public sealed class TeleportRulesTests
{
    private static readonly TeleportEntry Square = new()
    {
        Id = 1,
        MinLevel = 5,
        Destinations = new[] { new TeleportDestination(1, 500, " "), new TeleportDestination(3, 200, " ") },
    };

    [Fact]
    public void AnUnknownRowIsRefused()
    {
        Assert.Equal(NpcScript.Refused, TeleportRules.Check(null, 20, 1000, 0));
    }

    [Fact]
    public void LevelIsCheckedBeforeTheIndex()
    {
        Assert.Equal(TeleportRules.LevelTooLow, TeleportRules.Check(Square, 4, 1000, 9));
    }

    [Fact]
    public void AnIndexOutsideThePackedDestinationsIsRefused()
    {
        Assert.Equal(NpcScript.Refused, TeleportRules.Check(Square, 20, 1000, 2));
    }

    [Fact]
    public void MoneyBelowTheCostIsMoneyTooLow()
    {
        Assert.Equal(TeleportRules.MoneyTooLow, TeleportRules.Check(Square, 20, 199, 1));
        Assert.Equal(TeleportRules.Allowed, TeleportRules.Check(Square, 20, 200, 1));
    }

    [Theory]
    [InlineData(18, NpcScriptOperation.TeleportCheck)]
    [InlineData(19, NpcScriptOperation.TeleportStart)]
    [InlineData(148, NpcScriptOperation.ImageTeleportStart)]
    public void TeleportFunctionsAreClassified(uint functionId, NpcScriptOperation operation)
    {
        Assert.Equal(operation, NpcScript.Classify(functionId).Operation);
    }
}
