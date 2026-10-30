using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Combat;

public static class BossMeleeTally
{
    public static void Record(MonsterEntity boss, long damage, IReadOnlyList<int> attackSlots, MonsterParamEntry definition)
    {
        ArgumentNullException.ThrowIfNull(boss);
        ArgumentNullException.ThrowIfNull(attackSlots);
        ArgumentNullException.ThrowIfNull(definition);

        var cuts = ElementalAttack.ResistCuts(
            damage, attackSlots, element => SkillActions.ResistOf(definition, element) - boss.ResistLoss(element));
        long elemental = 0;
        for (var slot = 0; slot < cuts.Length; slot++)
        {
            if (cuts[slot] == 0) continue;
            boss.RecordElementDamage(slot, cuts[slot]);
            elemental += cuts[slot];
        }
        boss.RecordElementDamage(BossElements.Physical, damage - elemental);
    }
}
