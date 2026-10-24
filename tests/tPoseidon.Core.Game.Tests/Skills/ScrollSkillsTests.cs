using tPoseidon.Core.Game.Skills.Effects;

namespace tPoseidon.Core.Game.Tests.Skills;

public sealed class ScrollSkillsTests
{
    private static readonly int[] Bases = { 400, 50, 12, 30, 20, 10, 5, 8, 40, 900, 120, 60 };

    [Theory]
    [InlineData(1301, SkillEffectKinds.StatAp)]
    [InlineData(1302, SkillEffectKinds.StatMa)]
    [InlineData(1303, SkillEffectKinds.StatLk)]
    [InlineData(1304, SkillEffectKinds.StatHv)]
    [InlineData(1305, SkillEffectKinds.StatDa)]
    public void StatScrollsAddTheirValueFlat(int action, int stat)
    {
        var book = new SkillEffectBook(SkillEffectBook.PlayerCapacities);
        book.Register(new SkillEffect
        {
            ObjectId = 1, Kind = SkillEffectKinds.Find(action)!, SkillId = 7001, EsClass = 4, CasterEntityId = 1, OwnerEntityId = 1,
            Amount = 176f, DurationMs = 240000, Mode = SkillEffectMode.Applied,
        });

        Assert.Equal(176, book.Deltas(Bases, _ => null)[stat]);
    }
}
