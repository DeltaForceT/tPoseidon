using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class PassiveStatsTests
{
    private static SkillParam2Entry Passive(int id, int action, string table) => new()
    {
        Id = id, Type = SkillDefinition.PassiveType, MaxSkillLevel = 11, EsAction = action,
        EsaiiTable = "ESAII_" + table, EsaTable = "ESAction_" + table,
    };

    private static SkillTableRow Row(string table, int level, string column, int value) => new()
    {
        Table = "ESAction_" + table, Level = level, Columns = new Dictionary<string, string> { [column] = value.ToString(System.Globalization.CultureInfo.InvariantCulture) },
    };

    private static readonly SkillBook Book = new(
        new[] { Passive(4009, PassiveStats.Fitness, "Fitness"), Passive(2004, PassiveStats.ManaBottle, "ManaBottle"), Passive(3003, PassiveStats.BigBaggage, "BigBag1") },
        Array.Empty<SkillTableRow>(),
        new[]
        {
            Row("Fitness", 1, "HP", 500), Row("Fitness", 11, "HP", 2360),
            Row("ManaBottle", 3, "MP", 150),
            Row("BigBag1", 2, "WT", 230),
        });

    [Fact]
    public void LearnedStatPassivesAddTheirTableValueToTheMaximum()
    {
        var bonuses = PassiveStats.Bonuses(new Dictionary<int, int> { [4009] = 11, [2004] = 3, [3003] = 2 }, Book);

        Assert.Equal(2360, bonuses[SkillEffectKinds.StatHp]);
        Assert.Equal(150, bonuses[SkillEffectKinds.StatMp]);
        Assert.Equal(230, bonuses[SkillEffectKinds.StatWt]);
        Assert.Equal(0, bonuses[SkillEffectKinds.StatAp]);
    }

    [Fact]
    public void OnlyLearnedStatPassivesAreAnnounced()
    {
        Assert.Equal(new[] { SkillEffectKinds.StatHp }, PassiveStats.StatsOf(new Dictionary<int, int> { [4009] = 1 }, Book));
        Assert.Empty(PassiveStats.StatsOf(new Dictionary<int, int>(), Book));
    }
}
