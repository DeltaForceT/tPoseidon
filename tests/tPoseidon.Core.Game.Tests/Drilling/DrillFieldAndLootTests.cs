using tPoseidon.Core.Game.Drilling;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Drilling;

public sealed class DrillFieldAndLootTests
{
    private static ZoneGeometry Field(byte groundType) => new()
    {
        MinX = 0,
        MinY = 0,
        MaxX = 159,
        MaxY = 159,
        CellsX = 10,
        CellsY = 10,
        CellAttributes = new byte[100],
        GroundTypes = Enumerable.Repeat(groundType, 100).ToArray(),
        Regions = Array.Empty<ZoneRegion>(),
    };

    private static readonly int[] LaidDepths = { 10, 20 };
    private static readonly float[] EventRates = { 0f, 0.5f, 0f, 3f };

    private static readonly Dictionary<int, int> Natures = new() { [1] = 1, [11] = 6 };

    [Fact]
    public void ItemsAreLaidOnThePercentageOfDiggableTilesAtTensBetweenTheDepths()
    {
        var field = new DrillField(Field(1), new DrillZoneEntry(17, 10, 20, 60), Natures, new Random(3));

        Assert.Equal(100, field.DiggableCount);
        Assert.Equal(60, field.LaidCount);
        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                if (field.LaidDepth(x, y) is { } depth) Assert.Contains(depth, LaidDepths);
            }
        }
    }

    [Fact]
    public void NatureSixIsNotDiggable()
    {
        var field = new DrillField(Field(11), new DrillZoneEntry(17, 10, 20, 60), Natures, new Random(3));

        Assert.Equal(DrillRules.NotDiggableNature, field.NatureAt(0, 0));
        Assert.Equal(0, field.DiggableCount);
        Assert.Equal(0, field.LaidCount);
    }

    [Fact]
    public void TakingAnItemRefillsTheField()
    {
        var field = new DrillField(Field(1), new DrillZoneEntry(17, 10, 20, 60), Natures, new Random(3));
        var laid = Enumerable.Range(0, 100).Select(i => (X: i % 10, Y: i / 10)).First(cell => field.LaidDepth(cell.X, cell.Y) is not null);

        field.Take(laid.X, laid.Y, new Random(4));

        Assert.Equal(60, field.LaidCount);
    }

    [Fact]
    public void CategoryWeightsApplyTheEventRatesAndTheRowItemsFormTheSixthCategory()
    {
        var row = new MapItemZoneRow
        {
            ZoneId = 17,
            Tables = new[]
            {
                new MapItemCategory(string.Empty, 0), new MapItemCategory("Path_SW_EQ", 500), new MapItemCategory("Path_SW_Nor1", 4100),
                new MapItemCategory("Path_SW_Nor2", 400), new MapItemCategory(string.Empty, 0),
            },
            Items = new[] { new MapItemChance(11749, 1331), new MapItemChance(11485, 2487) },
        };
        var tables = new Dictionary<string, IReadOnlyList<MapItemChance>>
        {
            ["Path_SW_EQ"] = new[] { new MapItemChance(630016, 150) },
            ["Path_SW_Nor1"] = new[] { new MapItemChance(2001, 2000) },
        };

        var loot = new DrillLoot(row, tables, EventRates);

        Assert.Equal(750, loot.WeightOf(MapItemZoneRow.EquipCategory));
        Assert.Equal(4100, loot.WeightOf(MapItemZoneRow.FirstNormalCategory));
        Assert.Equal(3818, loot.WeightOf(MapItemZoneRow.RowCategory));
        Assert.Equal(9068, loot.TotalWeight);
        var rng = new Random(5);
        var seen = Enumerable.Range(0, 200).Select(_ => loot.Pop(_ => false, rng)).ToHashSet();
        Assert.Subset(new HashSet<int> { 0, 630016, 2001, 11749, 11485 }, seen);
    }
}
