using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class CompoundFramesTests
{
    private static readonly int[] TwoRecipes = { 101, 280 };
    private static readonly int[] OneResult = { 12005 };

    [Fact]
    public void TheListIsCountGradeAndIds()
    {
        Assert.Equal(new byte[] { 0x02, 0x01, 0x00, 0x65, 0x00, 0x18, 0x01 }, CompoundFrames.List(1, TwoRecipes));
    }

    [Fact]
    public void TheRecipeAnswerFollowsTheServerLayout()
    {
        var recipe = new CompoundRecipe
        {
            Id = 101,
            Type = CompoundRecipe.PotionType,
            Results = new[] { 12005 },
            Requirements = new[] { new CompoundRequirement(1000, 1) },
            Probability = 45,
            Fee = 3,
        };

        Assert.Equal(new byte[]
        {
            0x00, 0x00, 0x65, 0x00, 0x03, 0x00, 0x00, 0x00,
            0x01, 0xE5, 0x2E, 0x00, 0x00,
            0x01, 0xE8, 0x03, 0x00, 0x00, 0x01,
            0x2D,
        }, CompoundFrames.Recipe(recipe));
    }

    [Fact]
    public void ARefusalIsAnInt()
    {
        Assert.Equal(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF }, CompoundFrames.Refused(CompoundFrames.RefusedCode));
        Assert.Equal(new byte[] { 0x00, 0x00, 0x00, 0x00 }, CompoundFrames.Refused(CompoundFrames.NoBagSpaceCode));
    }

    [Fact]
    public void TheRateRequestCarriesModeCountAndMaterials()
    {
        var payload = new byte[]
        {
            0x65, 0x00, 0x0A, 0x03, 0x01,
            0x05, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0xE8, 0x03, 0x00, 0x00, 0x03, 0x00,
        };

        Assert.True(CompoundFrames.TryReadProbabilityRequest(payload, out var id, out var mode, out var count, out var materials));
        Assert.Equal((101, (byte)10, 3), (id, mode, count));
        Assert.Equal(new tPoseidon.Core.Game.Items.CompoundMaterial(0x0000_0001_0000_0005UL, 1000, 3), Assert.Single(materials));
        Assert.False(CompoundFrames.TryReadProbabilityRequest(payload.AsSpan(0, 18), out _, out _, out _, out _));
    }

    [Fact]
    public void TheResultListsIdsWasteCountsAndRecords()
    {
        var operation = new BagOperation(QuestResultFrames.Created, 1, 12005, 0x0000_0001_0000_0009UL, 2);
        var payload = CompoundFrames.Result(OneResult, 12000, 2, 1, new[] { operation });
        var record = QuestResultFrames.ItemOperation(operation);

        Assert.Equal(new byte[] { 0x01, 0xE5, 0x2E, 0x00, 0x00, 0xE0, 0x2E, 0x00, 0x00, 0x02, 0x01, 0x01 }, payload[..12]);
        Assert.Equal(record, payload[12..]);
    }
}
