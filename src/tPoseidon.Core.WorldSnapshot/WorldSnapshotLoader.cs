using tPoseidon.Core.Data;

namespace tPoseidon.Core.WorldSnapshot;

public static class WorldSnapshotLoader
{
    public sealed record LoadResult(int NpcsInstalled, int MonstersInstalled);

        public static LoadResult Load(
        WorldSnapshotDto snapshot,
        WorldEntityStore worldEntities,
        IdAllocator idAllocator,
        Func<NpcEntityDto, NpcEntity?> installNpc,
        Func<MonsterEntityDto, bool> spawnMonster)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(worldEntities);
        ArgumentNullException.ThrowIfNull(idAllocator);
        ArgumentNullException.ThrowIfNull(installNpc);
        ArgumentNullException.ThrowIfNull(spawnMonster);

        var npcsInstalled = 0;
        foreach (var dto in snapshot.Npcs)
        {
            idAllocator.Reserve(dto.EntityId);
            var entity = installNpc(dto);
            if (entity is not null)
            {
                worldEntities.Install(entity);
                npcsInstalled++;
            }
        }

        var monstersInstalled = 0;
        foreach (var dto in snapshot.Monsters)
        {
            idAllocator.Reserve(dto.EntityId);
            if (spawnMonster(dto)) monstersInstalled++;
        }

        idAllocator.AdvanceHighWaterTo(snapshot.NextEntityIdAfterBoot);

        return new LoadResult(npcsInstalled, monstersInstalled);
    }
}
