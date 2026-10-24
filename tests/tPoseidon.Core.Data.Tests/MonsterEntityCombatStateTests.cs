using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public class MonsterEntityCombatStateTests
{
    private static MonsterEntity NewMob() => new()
    {
        EntityId = 0x1F4B,
        MapId = 148,
        MonsterParamId = 1,
        Name = "Torobbie",
        SpawnX = 949,
        SpawnY = 2228,
        MaxHp = 60,
        Level = 1,
    };

    [Fact]
    public void NewMob_IsNotInCombat()
    {
        var mob = NewMob();
        Assert.False(mob.IsInCombat);
        Assert.Equal(0, mob.PrimaryAttackerEntityId);
        Assert.Empty(mob.SnapshotAttackers());
    }

    [Fact]
    public void AddAttacker_FlipsIsInCombat()
    {
        var mob = NewMob();
        Assert.True(mob.AddAttacker(0x0042));
        Assert.True(mob.IsInCombat);
        Assert.Equal(0x0042, mob.PrimaryAttackerEntityId);
    }

    [Fact]
    public void AddAttacker_Idempotent()
    {
        var mob = NewMob();
        Assert.True(mob.AddAttacker(0x0042));
        Assert.False(mob.AddAttacker(0x0042));
        Assert.Single(mob.SnapshotAttackers());
    }

    [Fact]
    public void AddAttacker_ZeroIgnored()
    {
        var mob = NewMob();
        Assert.False(mob.AddAttacker(0));
        Assert.False(mob.IsInCombat);
    }

    [Fact]
    public void RemoveAttacker_ClearsCombatWhenLast()
    {
        var mob = NewMob();
        mob.AddAttacker(0x0042);
        Assert.True(mob.RemoveAttacker(0x0042));
        Assert.False(mob.IsInCombat);
        Assert.Equal(0, mob.PrimaryAttackerEntityId);
    }

    [Fact]
    public void MultipleAttackers_AllTracked()
    {
        var mob = NewMob();
        mob.AddAttacker(0x0042);
        mob.AddAttacker(0x0043);
        mob.AddAttacker(0x0044);
        var snapshot = mob.SnapshotAttackers();
        Assert.Equal(3, snapshot.Count);
        Assert.Contains((ushort)0x0042, snapshot);
        Assert.Contains((ushort)0x0043, snapshot);
        Assert.Contains((ushort)0x0044, snapshot);
    }

    [Fact]
    public void RemovePartial_LeavesOthersAndKeepsCombat()
    {
        var mob = NewMob();
        mob.AddAttacker(0x0042);
        mob.AddAttacker(0x0043);
        mob.RemoveAttacker(0x0042);
        Assert.True(mob.IsInCombat);
        Assert.Equal(0x0043, mob.PrimaryAttackerEntityId);
    }

    [Fact]
    public void PrimaryAttacker_TracksLastAddCall()
    {
        var mob = NewMob();
        mob.AddAttacker(0x0042);
        Assert.Equal(0x0042, mob.PrimaryAttackerEntityId);
        mob.AddAttacker(0x0043);
        Assert.Equal(0x0043, mob.PrimaryAttackerEntityId);
        mob.AddAttacker(0x0044);
        Assert.Equal(0x0044, mob.PrimaryAttackerEntityId);
    }

    [Fact]
    public void PrimaryAttacker_FallsBackWhenLastRemoved()
    {
        var mob = NewMob();
        mob.AddAttacker(0x0042);
        mob.AddAttacker(0x0043);
        Assert.Equal(0x0043, mob.PrimaryAttackerEntityId);
        mob.RemoveAttacker(0x0043);
        Assert.Equal(0x0042, mob.PrimaryAttackerEntityId);
    }

    [Fact]
    public void PrimaryAttacker_ReAddRefreshesLast()
    {
        var mob = NewMob();
        mob.AddAttacker(0x0042);
        mob.AddAttacker(0x0043);
        mob.AddAttacker(0x0042);
        Assert.Equal(0x0042, mob.PrimaryAttackerEntityId);
    }

    [Fact]
    public void ClearAttackers_RemovesEverything()
    {
        var mob = NewMob();
        mob.AddAttacker(0x0042);
        mob.AddAttacker(0x0043);
        mob.ClearAttackers();
        Assert.False(mob.IsInCombat);
        Assert.Empty(mob.SnapshotAttackers());
    }

    [Fact]
    public async Task ConcurrentAddRemove_StaysConsistent()
    {
        var mob = NewMob();
        var tasks = new List<Task>();
        for (var i = 1; i <= 50; i++)
        {
            var id = (ushort)i;
            tasks.Add(Task.Run(() =>
            {
                mob.AddAttacker(id);
                mob.RemoveAttacker(id);
            }));
        }
        await Task.WhenAll(tasks);
        Assert.False(mob.IsInCombat);
    }
}
