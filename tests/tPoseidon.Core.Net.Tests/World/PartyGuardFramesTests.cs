using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Skills.Effects;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PartyGuardFramesTests
{
    [Fact]
    public void AGuardedBlowCarriesKindSixAndTheBlocksLeft()
    {
        var guarded = new MonsterBattleSwung(0x5000, 1, 2, 0x10, 3, 4, 0, SkillEffectKinds.GuardedResult, false, 4);

        var hit = MonsterBattleFrames.Encode(guarded, 7).Single(frame => frame.Opcode == Opcode.S2C_OP_002A_TBD).Payload;

        Assert.Equal(0, BitConverter.ToInt32(hit, 2));
        Assert.Equal(SkillEffectKinds.GuardedResult, BitConverter.ToInt32(hit, 10));
        Assert.Equal(4, BitConverter.ToUInt16(hit, 26));
    }

    [Fact]
    public void PartyGuardSpreadsAsTheMemberGuardWithCharges()
    {
        var cast = SkillEffectKinds.Find(SkillEffectKinds.PartyGuard)!;
        var member = SkillEffectKinds.Find(cast.SpreadsAs)!;

        Assert.False(cast.OnTarget);
        Assert.Equal(SkillEffectKinds.PartyGuardOnMember, member.ActionId);
        Assert.True(member.GuardsHits);
        Assert.Equal(SkillEffectTail.Duration, member.Tail);
    }
}
