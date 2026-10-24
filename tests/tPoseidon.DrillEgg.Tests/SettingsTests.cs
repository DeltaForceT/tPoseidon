using tPoseidon.Core.Common;
using tPoseidon.DrillEgg.Config;
using tPoseidon.DrillEgg.Prizes;

namespace tPoseidon.DrillEgg.Tests;

public sealed class SettingsTests
{
    private static readonly string Pack = Path.Combine(RepoRoot.Path, "src", "tPoseidon.DrillEgg", "pack");

    public static TheoryData<string> PackedSettings()
    {
        var files = new TheoryData<string> { Path.Combine(Pack, SettingsLoader.DefaultFileName) };
        foreach (var example in Directory.EnumerateFiles(Path.Combine(Pack, "examples"), "*.json")) files.Add(example);
        return files;
    }

    [Theory]
    [MemberData(nameof(PackedSettings))]
    public void EveryShippedSettingsFile_IsValid(string file)
    {
        var loaded = SettingsLoader.Load(file);

        Assert.Equal(8080, loaded.Settings.Port);
    }

    [Fact]
    public void TheDefault_IsSinglePlayerFreePlayOnThisMachine()
    {
        var settings = SettingsLoader.Load(Path.Combine(Pack, SettingsLoader.DefaultFileName)).Settings;

        Assert.Equal(ListenAddresses.Localhost, settings.Listen);
        Assert.Equal(ConnectorNames.Local, settings.Players.Connector);
        Assert.Equal(ConnectorNames.Free, settings.Coupons.Connector);
        Assert.Equal(ConnectorNames.Local, settings.Delivery.Connector);
    }

    [Fact]
    public void Paths_AreRelativeToTheSettingsFolder()
    {
        using var folder = new TempFolder();
        var loaded = Fixtures.Settings(folder.Path, Fixtures.Prizes);

        Assert.Equal(Path.Combine(folder.Path, "myshop.swf"), loaded.SwfPath);
        Assert.Equal(Path.Combine(folder.Path, "art"), loaded.ArtFolder);
        Assert.Equal(Path.Combine(folder.Path, "pictures"), loaded.PicturesFolder);
    }

    [Fact]
    public void AMisspelledSetting_IsReportedWithItsLine()
    {
        using var folder = new TempFolder();
        var error = Assert.Throws<SettingsException>(() => SettingsLoader.Parse("{\n  \"prot\": 9000\n}", folder.Path, "drill-egg.json"));

        Assert.Contains("line 2", error.Message, StringComparison.Ordinal);
        Assert.Contains("prot", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CommentsAndTrailingCommas_AreAllowed()
    {
        using var folder = new TempFolder();
        var loaded = SettingsLoader.Parse("{ // my notes\n \"port\": 9000, " + Fixtures.Prizes + ", }", folder.Path, "x.json");

        Assert.Equal(9000, loaded.Settings.Port);
    }

    [Theory]
    [InlineData("\"port\": 0", "port must be between")]
    [InlineData("\"listen\": \"everywhere\"", "listen must be")]
    [InlineData("\"coupons\": { \"connector\": \"tposeidon\" }", "delivery.connector must be \"tposeidon\"")]
    [InlineData("\"coupons\": { \"connector\": \"sql\", \"database\": \"game\", \"count\": \"x\", \"spend\": \"y\" }", "is not listed under databases")]
    [InlineData("\"players\": { \"connector\": \"sql\", \"database\": \"game\" }, \"databases\": { \"game\": { \"provider\": \"sqlserver\", \"connection\": \"Server=x\" } }", "players.query is missing")]
    [InlineData("\"databases\": { \"game\": { \"provider\": \"oracle\", \"connection\": \"x\" } }", "databases.game.provider must be one of")]
    [InlineData("\"delivery\": { \"connector\": \"http\", \"url\": \"ftp://example.org/x\" }", "must be a full http:// or https:// address")]
    [InlineData("\"delivery\": { \"connector\": \"tposeidon\" }, \"tposeidon\": { \"connection\": \"Host=x\" }", "needs the player's emulator account id")]
    [InlineData("\"players\": { \"connector\": \"tposeidon\" }", "tposeidon.connection must hold")]
    [InlineData("\"areas\": { \"65\": { \"prizes\": [] } }", "is not a dig site")]
    [InlineData("\"coupons\": { \"connector\": \"coins\" }", "coupons.connector must be one of")]
    [InlineData("\"deepDrill\": { \"rareBoost\": 0 }", "rareBoost must be 1 or more")]
    public void Mistakes_AreExplained(string setting, string expected)
    {
        using var folder = new TempFolder();
        var error = Assert.Throws<SettingsException>(() => Fixtures.Settings(folder.Path, setting + ", " + Fixtures.Prizes));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{ "id": 1, "name": "A", "weight": 1 }, { "id": 1, "name": "A", "weight": 2 }""", "is listed twice")]
    [InlineData("""{ "id": 1, "name": "A", "weight": 0 }""", "weight must be above 0")]
    [InlineData("""{ "id": 1, "name": "", "weight": 1 }""", "name is empty")]
    [InlineData("""{ "id": 1, "name": "A", "weight": 1, "picture": "../secret.png" }""", "picture must be a file name")]
    [InlineData("""{ "id": 1, "name": "A", "weight": 2147483647 }, { "id": 2, "name": "B", "weight": 2 }""", "add up to more than")]
    public void PrizeMistakes_AreExplained(string prizes, string expected)
    {
        using var folder = new TempFolder();
        var error = Assert.Throws<SettingsException>(() => Fixtures.Settings(folder.Path, "\"prizes\": [" + prizes + "]"));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void NoPrizes_IsOnlyAllowedWhenTheEmulatorHoldsThePools()
    {
        using var folder = new TempFolder();

        Assert.Throws<SettingsException>(() => Fixtures.Settings(folder.Path, "\"prizes\": []"));
        var loaded = Fixtures.Settings(folder.Path, """
            "tposeidon": { "connection": "Host=localhost" },
            "players": { "connector": "tposeidon" },
            "coupons": { "connector": "tposeidon" },
            "delivery": { "connector": "tposeidon" }
            """);
        Assert.True(SettingsValidator.UsesStorePools(loaded.Settings));
    }

    [Fact]
    public void ThePrizeCatalog_BoostsRarePrizesInTheDeepDrill()
    {
        using var folder = new TempFolder();
        var catalog = PrizeCatalog.From(Fixtures.Settings(folder.Path, Fixtures.Prizes).Settings);

        Assert.Equal([3, 1], catalog.Pool(61, false).Select(p => p.Weight));
        Assert.Equal([3, 3], catalog.Pool(61, true).Select(p => p.Weight));
        Assert.Equal("Crystal Egg", catalog.NameOf(2));
        Assert.Equal("Item 77", catalog.NameOf(77));
        Assert.Empty(catalog.Pool(60, false));
    }

    [Fact]
    public void ADigSite_CanHaveItsOwnPrizesAndExactDeepWeights()
    {
        using var folder = new TempFolder();
        var catalog = PrizeCatalog.From(Fixtures.Settings(folder.Path, Fixtures.Prizes + """
            , "areas": { "64": { "prizes": [ { "id": 9, "name": "Ruby", "weight": 5, "deepWeight": 50, "count": 3 } ] } }
            """).Settings);

        var deep = Assert.Single(catalog.Pool(64, true));
        Assert.Equal(50, deep.Weight);
        Assert.Equal(3, deep.Count);
        Assert.Equal(5, Assert.Single(catalog.Pool(64, false)).Weight);
        Assert.Equal(2, catalog.Pool(63, false).Count);
        Assert.Equal(8, catalog.Pools.Count());
    }
}
