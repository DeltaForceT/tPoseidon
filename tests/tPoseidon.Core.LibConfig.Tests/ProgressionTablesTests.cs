namespace tPoseidon.Core.LibConfig.Tests;

public class ProgressionTablesTests
{
    private static readonly string KrLibConfigPath = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "data", "libconfig_kr_en.xml"));

    private static readonly int[] MemoryPortItems = [7708, 19659, 19805, 19810];
    private static readonly int[] KillPoints = [2, 1, 0, 0];

    private static bool KrAvailable => File.Exists(KrLibConfigPath);

    [SkippableFact]
    public void HonorTitlesCarryTheQuestKeysAndCounters()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var titles = new LibConfigXmlReader(KrLibConfigPath).LoadHonorTitles();

        Assert.Equal(73, titles.Count);
        Assert.All(titles, title => Assert.Equal(7, title.TitleType));
        Assert.Equal(new HonorTitleEntry(1, 7, 1, 4065, 1, 0, 0), titles.Single(title => title.Id == 1));
        Assert.Equal(new HonorTitleEntry(9, 7, 2, 1054, 0, 30, 105), titles.Single(title => title.Id == 9));
    }

    [SkippableFact]
    public void MemoryPortsAreFourFiveSlotTypeZeroItems()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var rows = new LibConfigXmlReader(KrLibConfigPath).LoadMemoryPorts();

        Assert.Equal(MemoryPortItems, rows.Select(row => row.ItemId).Order());
        Assert.All(rows, row => Assert.Equal((0, 5), (row.Type, row.SlotCount)));
    }

    [SkippableFact]
    public void FortuneTablesCarryCardsFeesSeedsAndTitleRules()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var tables = new LibConfigXmlReader(KrLibConfigPath).LoadFortuneTables();

        Assert.Equal(118, tables.Cards.Count);
        var card = tables.Cards[21003];
        Assert.Equal(("AP", 16f, 3600), (card.Attribute, card.Value, card.Duration));
        Assert.Equal(new FortuneCross("DX", -0.33f), card.Cross[2]);
        Assert.Equal(15, tables.FeeByUse.Count);
        Assert.Equal(1000, tables.FeeByUse[1]);
        Assert.Equal(16384000, tables.FeeByUse[15]);
        Assert.Equal(0.3f, tables.Rules[FortuneTables.NeedleSelectCardSeed]);
        Assert.Equal(1f, tables.Rules[FortuneTables.MyCardSelectSeed]);
        Assert.Equal(239, tables.Titles.Count);
        var first = tables.Titles.Single(rule => rule.Index == 6);
        Assert.Equal(133, first.TitleId);
        Assert.Equal(new FortuneTitleCondition(2, 21110), first.Conditions[0]);
    }

    [SkippableFact]
    public void FortuneBasicCardsAreTheFortyOneLowCardNumbersAndJokerRowsFollowRowOrder()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var reader = new LibConfigXmlReader(KrLibConfigPath);
        var basic = reader.LoadItemParam2()
            .Where(item => item.Type == 35 && item.SubType == 70 && item.CardNum is >= 0 and <= 3)
            .Select(item => item.Id)
            .Distinct()
            .ToArray();
        Assert.Equal(41, basic.Length);
        Assert.Contains(21001, basic);
        Assert.Contains(21114, basic);
        Assert.DoesNotContain(21005, basic);

        var jokers = reader.LoadFortuneTables().CardsInRowOrder.Where(card => card.JokerProb > 0f).ToArray();
        Assert.Equal(66, jokers.Length);
        Assert.Equal(21.9, jokers.Sum(card => (double)card.JokerProb), 3);
        Assert.Equal(21001, jokers[0].Id);
    }

    [SkippableFact]
    public void AutoDrillPetsAreSevenItemsWithCoolTimes()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var pets = new LibConfigXmlReader(KrLibConfigPath).LoadDrillTables().AutoDrillPets;

        Assert.Equal(7, pets.Count);
        Assert.Equal(3000, pets[1566]);
        Assert.Equal(3000, pets[90116]);
    }

    [SkippableFact]
    public void SoulSeedTablesCarrySeedsEventsAndGuardianNames()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var tables = new LibConfigXmlReader(KrLibConfigPath).LoadSoulSeedTables();

        Assert.Equal(Enumerable.Range(15000, 4), tables.GuardianTypesBySeed[38000]);
        Assert.Equal(Enumerable.Range(15004, 4), tables.GuardianTypesBySeed[38001]);
        Assert.Equal(55, tables.EventsByType.Count);
        var kill = tables.EventsByType[10];
        Assert.Equal((5, 5, false), (kill.Rate, kill.MaximumLevel, kill.Special));
        Assert.Equal(KillPoints, kill.Points);
        Assert.True(tables.EventsByType[41].Special);
        Assert.Equal(8, tables.GuardianNames.Count);
    }

    [SkippableFact]
    public void DisguiseKitsMapItemsToCharacterTypes()
    {
        Skip.IfNot(KrAvailable, $"KR libconfig not at {KrLibConfigPath}");

        var kits = new LibConfigXmlReader(KrLibConfigPath).LoadTransScrolls();

        Assert.Equal(82, kits.Count);
        Assert.Equal(2075, kits.Single(kit => kit.ItemId == 37500).TransChrType);
        Assert.All(kits, kit => Assert.InRange(kit.TransChrType, 1, 0x4671));
    }
}
