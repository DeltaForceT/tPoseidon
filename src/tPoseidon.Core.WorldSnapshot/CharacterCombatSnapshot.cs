using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.WorldSnapshot;

public static class CharacterCombatSnapshot
{
    public static CharacterCombatInfoDto ToDto(CharacterCombatInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return new CharacterCombatInfoDto
        {
            Id = info.Id,
            MinAttackRange = info.MinAttackRange,
            MaxAttackRange = info.MaxAttackRange,
            ChaseRange = info.ChaseRange,
            WalkSpeed = info.WalkSpeed,
            RunSpeed = info.RunSpeed,
            AttackMs = info.AttackMs,
            Attack2Ms = info.Attack2Ms,
            AttackReadyMs = info.AttackReadyMs,
        };
    }

    public static Dictionary<int, CharacterCombatInfo> ReadAll(WorldSnapshotDto snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var byId = new Dictionary<int, CharacterCombatInfo>();
        if (snapshot.CombatInfos is null) return byId;
        foreach (var dto in snapshot.CombatInfos)
        {
            byId[dto.Id] = new CharacterCombatInfo
            {
                Id = dto.Id,
                MinAttackRange = dto.MinAttackRange,
                MaxAttackRange = dto.MaxAttackRange,
                ChaseRange = dto.ChaseRange,
                WalkSpeed = dto.WalkSpeed,
                RunSpeed = dto.RunSpeed,
                AttackMs = dto.AttackMs,
                Attack2Ms = dto.Attack2Ms,
                AttackReadyMs = dto.AttackReadyMs,
            };
        }
        return byId;
    }
}
