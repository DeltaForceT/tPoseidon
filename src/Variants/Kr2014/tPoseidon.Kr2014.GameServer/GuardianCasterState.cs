using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Guardian;
using tPoseidon.Core.Game.Stats;

namespace tPoseidon.Kr2014.GameServer;

internal static class GuardianCasterState
{
    public const ulong NoCharacterId = ulong.MaxValue;

    public static InGameSessionState Refresh(InGameSessionState owner, GuardianActor guardian, GuardianState state, IReadOnlyList<int> stats, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(guardian);
        if (owner.GuardianCaster is { Stats: { } held } existing && existing.PlayerEntityId == guardian.EntityId && held.Level == state.Level)
        {
            var (x, y) = guardian.PositionAt(now);
            existing.PlayerX = (ushort)x;
            existing.PlayerY = (ushort)y;
            existing.MapId = guardian.MapId;
            return existing;
        }
        owner.GuardianCaster = Create(owner, guardian, state, stats, now);
        return owner.GuardianCaster;
    }

    private static InGameSessionState Create(InGameSessionState owner, GuardianActor guardian, GuardianState state, IReadOnlyList<int> stats, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(guardian);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(stats);
        var ownerCharacter = owner.Character ?? throw new ArgumentException("The owner has no character.", nameof(owner));
        var maxHp = stats[GuardianStats.HpIndex];
        var snapshot = new Character
        {
            CharacterId = NoCharacterId,
            OwnerAccountId = ownerCharacter.OwnerAccountId,
            Slot = ownerCharacter.Slot,
            Name = state.Name,
            Species = ownerCharacter.Species,
            FType = ownerCharacter.FType,
            Level = (ushort)state.Level,
            CurrentHp = maxHp,
        };
        var (x, y) = guardian.PositionAt(now);
        return new InGameSessionState
        {
            MapId = guardian.MapId,
            SendFrameAsync = owner.SendFrameAsync,
            PlayerEntityId = guardian.EntityId,
            PlayerX = (ushort)x,
            PlayerY = (ushort)y,
            Character = snapshot,
            Stats = new StatManager(snapshot),
            PlayerAp = stats[GuardianStats.ApIndex],
            PlayerAc = stats[GuardianStats.AcIndex],
            PlayerMa = stats[GuardianStats.MaIndex],
            PlayerLk = stats[GuardianStats.LkIndex],
            PlayerDa = stats[GuardianStats.DaIndex],
            PlayerDx = stats[GuardianStats.DxIndex],
            PlayerDp = stats[GuardianStats.DpIndex],
            PlayerHv = stats[GuardianStats.HvIndex],
            PlayerMaxHp = maxHp,
            Skills = state.Skills.Where(skill => skill.Type == GuardianSkillType.Card).DistinctBy(skill => skill.SkillId).ToDictionary(skill => skill.SkillId, skill => (int)skill.Level),
            AlwaysHits = true,
            SkillOwner = owner,
        };
    }
}
