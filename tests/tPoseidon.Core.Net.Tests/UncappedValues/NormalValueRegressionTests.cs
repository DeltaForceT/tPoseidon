namespace tPoseidon.Core.Net.Tests.UncappedValues;

public sealed class NormalValueRegressionTests
{
    private static readonly string GoldenPath = Path.Combine(AppContext.BaseDirectory, "UncappedValues", "NormalValueSnapshot.golden.txt");

    [Fact]
    public void NormalRangeResultsAndPacketBytesMatchTheCapturedBaseline()
    {
        var expected = File.ReadAllLines(GoldenPath);
        var actual = NormalValueSnapshot.Lines().ToArray();

        Assert.Equal(expected.Length, actual.Length);
        var differences = new List<string>();
        for (var index = 0; index < expected.Length; index++)
        {
            if (expected[index] != actual[index]) differences.Add($"{index + 1}: expected [{expected[index]}] actual [{actual[index]}]");
        }
        Assert.True(differences.Count == 0, $"{differences.Count} lines differ. First: {string.Join(Environment.NewLine, differences.Take(10))}");
    }
}
