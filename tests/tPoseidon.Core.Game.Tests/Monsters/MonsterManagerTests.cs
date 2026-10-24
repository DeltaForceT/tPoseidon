using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.GameTests.Monsters;

public class MonsterManagerTests
{
    private const ushort CoralTownMapId = 148;
    private const int TorobbieId = 2183;

    private static MonsterManager Make(out InMemoryMonsterParamLookup catalog)
    {
        catalog = new InMemoryMonsterParamLookup(new[]
        {
            new MonsterParamEntry { Id = TorobbieId, Name = "Torobbie", Level = 1, MaxHpLv = 100, ApLv = 1, DpLv = 1, Exp = 48, GMin = 30, GMax = 30 },
            new MonsterParamEntry { Id = 16250, Name = "Shadow Tottochi", Level = 5, MaxHpLv = 6, IsBoss = true, GMin = 50, GMax = 100 },
        });
        return new MonsterManager(catalog, new IdAllocator());
    }

    [Fact]
    public void Spawn_ValidId_AppearsInCatalog()
    {
        var mm = Make(out _);
        MonsterEntity? captured = null;
        mm.MonsterSpawned += e => captured = e;

        var mob = mm.Spawn(TorobbieId, CoralTownMapId, 2744, 1144);

        Assert.NotNull(mob);
        Assert.Equal("Torobbie", mob!.Name);
        Assert.Equal(100, mob.MaxHp);
        Assert.Equal(100, mob.CurrentHp);
        Assert.False(mob.IsDead);
        Assert.Equal(mob, captured);
        Assert.Equal(1, mm.Count);
    }

    [Fact]
    public void Spawn_UnknownId_ReturnsNull()
    {
        var mm = Make(out _);
        Assert.Null(mm.Spawn(99999, CoralTownMapId, 0, 0));
        Assert.Equal(0, mm.Count);
    }

    [Fact]
    public void Spawn_BossMob_FlagPropagates()
    {
        var mm = Make(out _);
        var boss = mm.Spawn(16250, CoralTownMapId, 1000, 1500);
        Assert.NotNull(boss);
        Assert.True(boss!.IsBoss);
    }

    [Fact]
    public void GetByMap_FiltersByMapId_AndExcludesDead()
    {
        var mm = Make(out _);
        mm.Spawn(TorobbieId, CoralTownMapId, 1, 1);
        mm.Spawn(TorobbieId, CoralTownMapId, 2, 2);
        mm.Spawn(TorobbieId, mapId: 999, 3, 3);

        var coral = mm.GetByMap(CoralTownMapId);
        Assert.Equal(2, coral.Count);

        var first = coral.First();
        mm.ApplyDamage(first.EntityId, attackerEntityId: 100, damage: 9999);
        Assert.Single(mm.GetByMap(CoralTownMapId));
    }

    [Fact]
    public void ApplyDamage_PartialDamage_FiresDamagedEvent_NotKilled()
    {
        var mm = Make(out _);
        var mob = mm.Spawn(TorobbieId, CoralTownMapId, 0, 0)!;
        var damaged = false;
        var killed = false;
        mm.MonsterDamaged += (_, _, _) => damaged = true;
        mm.MonsterKilled += (_, _) => killed = true;

        var result = mm.ApplyDamage(mob.EntityId, attackerEntityId: 200, damage: 50);

        Assert.Equal(DamageResult.Damaged, result);
        Assert.True(damaged);
        Assert.False(killed);
        Assert.Equal(50, mob.CurrentHp);
    }

    [Fact]
    public void ApplyDamage_LethalDamage_FiresKilledEvent()
    {
        var mm = Make(out _);
        var mob = mm.Spawn(TorobbieId, CoralTownMapId, 0, 0)!;
        ushort? killerId = null;
        mm.MonsterKilled += (m, atk) => killerId = atk;

        var result = mm.ApplyDamage(mob.EntityId, attackerEntityId: 0x1234, damage: 9999);

        Assert.Equal(DamageResult.Killed, result);
        Assert.Equal((ushort)0x1234, killerId);
        Assert.True(mob.IsDead);
        Assert.Equal(0, mob.CurrentHp);
    }

    [Fact]
    public void ApplyDamage_AlreadyDead_ReturnsAlreadyDead()
    {
        var mm = Make(out _);
        var mob = mm.Spawn(TorobbieId, CoralTownMapId, 0, 0)!;
        mm.ApplyDamage(mob.EntityId, 200, 9999);

        var result = mm.ApplyDamage(mob.EntityId, attackerEntityId: 200, damage: 50);

        Assert.Equal(DamageResult.AlreadyDead, result);
    }

    [Fact]
    public void ApplyDamage_UnknownEntity_ReturnsNotFound()
    {
        var mm = Make(out _);
        var result = mm.ApplyDamage(entityId: 0xFFFE, attackerEntityId: 100, damage: 10);
        Assert.Equal(DamageResult.NotFound, result);
    }

    [Fact]
    public void Remove_DeallocatesEntityId_AllowsReuse()
    {
        var mm = Make(out _);
        var first = mm.Spawn(TorobbieId, CoralTownMapId, 0, 0)!;
        Assert.True(mm.Remove(first.EntityId));
        Assert.Equal(0, mm.Count);

        var second = mm.Spawn(TorobbieId, CoralTownMapId, 0, 0)!;
        Assert.Equal(first.EntityId, second.EntityId);
    }

    [Fact]
    public void MonsterEntity_ApplyDamage_OverflowDamage_DoesNotGoNegative()
    {
        var mob = new MonsterEntity
        {
            EntityId = 1,
            MapId = CoralTownMapId,
            MonsterParamId = TorobbieId,
            Name = "Torobbie",
            SpawnX = 0,
            SpawnY = 0,
            MaxHp = 100,
            Level = 2,
        };
        mob.InitHp(100);

        var dealt = mob.ApplyDamage(int.MaxValue);

        Assert.Equal(100, dealt);
        Assert.Equal(0, mob.CurrentHp);
        Assert.True(mob.IsDead);
    }
}
