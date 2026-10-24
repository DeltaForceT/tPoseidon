using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class NpcIdleProfilesTests
{
    private const int GeneralType = 1002;
    private const int ShopType = 5001;
    private const int SkillShopType = 6300;

    private static readonly NpcIdleProfiles Profiles = new(
        new InMemoryNpcActionScriptLookup(new[] { Script(GeneralType), Script(ShopType), Script(SkillShopType) }),
        new InMemoryNpcActionScriptLookup(new[] { Script(ShopType) }),
        new InMemoryNpcActionScriptLookup(new[] { Script(SkillShopType) }),
        new HashSet<int> { ShopType },
        new HashSet<int> { SkillShopType });

    private static NpcActionScriptEntry Script(int type) => new()
    {
        Type = type,
        MaxCnt = 1,
        Steps = new[] { new NpcActionScriptStep(Action: 1, Loop: 1, AniTime: 5, Speech: $"type {type}") },
    };

    private static NpcEntity NpcOfType(int chrType) => new()
    {
        EntityId = 0x0101,
        MapId = 1,
        ChrType = chrType,
        PosId = 1,
        Spawn = NpcPoint.Xy(100, 100),
    };

    [Fact]
    public void GeneralNpc_UsesTheNpcTableAndTheTenSecondWait()
    {
        var profile = Profiles.For(NpcOfType(GeneralType));

        Assert.Equal(GeneralType, profile.Script?.Type);
        Assert.Equal(TimeSpan.FromSeconds(10), profile.WaitWithoutMotion);
    }

    [Fact]
    public void ShopNpc_UsesTheShopTableAndTheTenSecondWait()
    {
        var profile = Profiles.For(NpcOfType(ShopType));

        Assert.Equal(ShopType, profile.Script?.Type);
        Assert.Equal(TimeSpan.FromSeconds(10), profile.WaitWithoutMotion);
    }

    [Fact]
    public void SkillShopNpc_UsesTheSkillShopTableAndTheFiveSecondWait()
    {
        var profile = Profiles.For(NpcOfType(SkillShopType));

        Assert.Equal(SkillShopType, profile.Script?.Type);
        Assert.Equal(TimeSpan.FromSeconds(5), profile.WaitWithoutMotion);
    }

    [Fact]
    public void NpcWithoutARow_HasNoScript()
    {
        var profile = Profiles.For(NpcOfType(1999));

        Assert.Null(profile.Script);
    }
}
