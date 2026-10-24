using tPoseidon.Core.Game.Items;

namespace tPoseidon.Core.Game.Tests.Items;

public sealed class UniqueItemKindsTests
{
    private const int AbsoluteOptions = 1 | 16 | 256 | 512 | 4096 | 131072 | 1048576;
    private const int NormalOptions = 1 | 16 | 2097152;

    [Fact]
    public void KindComesFromTheOptionsBits()
    {
        Assert.Equal(UniqueItemKinds.Absolute, UniqueItemKinds.KindOf(AbsoluteOptions));
        Assert.Equal(UniqueItemKinds.Normal, UniqueItemKinds.KindOf(NormalOptions));
        Assert.Equal(UniqueItemKinds.Event, UniqueItemKinds.KindOf(0x8000000));
        Assert.Equal(UniqueItemKinds.None, UniqueItemKinds.KindOf(1 | 16));
    }

    [Fact]
    public void OnlyAbsoluteUniquesAreWithdrawnOnRelease()
    {
        Assert.True(UniqueItemKinds.WithdrawnOnRelease(AbsoluteOptions));
        Assert.False(UniqueItemKinds.WithdrawnOnRelease(NormalOptions));
        Assert.False(UniqueItemKinds.WithdrawnOnRelease(1048576));
    }

    [Fact]
    public void BossAndNonresidentBossKillsCountForTheSeal()
    {
        Assert.True(UniqueItemKinds.KilledAsBoss(3));
        Assert.True(UniqueItemKinds.KilledAsBoss(7));
        Assert.False(UniqueItemKinds.KilledAsBoss(1));
        Assert.False(UniqueItemKinds.KilledAsBoss(8));
        var now = new DateTimeOffset(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);
        Assert.True(tPoseidon.Core.Data.UniqueItemRules.SealCredited(3001, now, now));
        Assert.False(tPoseidon.Core.Data.UniqueItemRules.SealCredited(2214, now.AddDays(1), now));
        Assert.False(tPoseidon.Core.Data.UniqueItemRules.SealCredited(2215, now.AddDays(1), now));
        Assert.False(tPoseidon.Core.Data.UniqueItemRules.SealCredited(3001, now.AddSeconds(-1), now));
        Assert.False(tPoseidon.Core.Data.UniqueItemRules.SealCredited(3001, null, now));
    }
}
