using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Combat;

internal sealed class CastInitFrameBuilder : ICastFrameBuilder
{
    private const byte CastFlagA = 0x02;
    private const byte CastFlagB = 0x01;
    private const byte CastFlagC = 0x01;

    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_00F2_TBD,
        new Packet00F2Tbd
        {
            PlayerEntityId = ctx.CasterEid,
            FieldAt02Tbd = CastFlagA,
            FieldAt03Tbd = ctx.Skill.SkillId,
            FieldAt05Tbd = CastFlagB,
            FieldAt06Tbd = CastFlagC,
            TargetEntityId = ctx.TargetEid,
        }.EncodePayload()
    );
}

internal sealed class CastHaltFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_0019_TBD,
        new HaltPacket
        {
            EntityId = ctx.CasterEid,
            X = ctx.CasterX,
            Y = ctx.CasterY,
        }.EncodePayload()
    );
}

internal sealed class CastEndFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_00F0_TBD,
        new Packet00F0Tbd
        {
            FieldAt00Tbd = ctx.Skill.SkillId,
            FieldAt02Tbd = ctx.Slot,
        }.EncodePayload()
    );
}

internal sealed class CastConfirmFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_00E0_TBD,
        new Packet00E0Tbd
        {
            PlayerEntityId = ctx.CasterEid,
            TargetEntityId = ctx.TargetEid,
            RendererFactoryId = ctx.Skill.RendererFactoryId,
            SkillId = ctx.Skill.SkillId,
        }.EncodePayload()
    );
}

internal sealed class CastHitTargetFrameBuilder : ICastFrameBuilder
{
    private const uint HitKindPlayerToMob = 0x00000001u;

    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_002A_TBD,
        new Packet002ATbd
        {
            VictimEntityId = ctx.TargetEid,
            AnimSubOp = ctx.Damage,
            HitFlag = Packet002ATbd.HitFlagNormal,
            MarkerAt10 = Packet002ATbd.HitMarkerA,
            MarkerAt14 = Packet002ATbd.HitMarkerB,
            HitKind = HitKindPlayerToMob,
        }.EncodePayload()
    );
}

internal sealed class CastProjectileSpawnFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_000C_TBD,
        new Packet000CTbd
        {
            TargetEntityId = ctx.TargetEid,
            ProjectileEntityId = ctx.ProjectileEid,
            TargetX = ctx.TargetX,
            TargetY = ctx.TargetY,
        }.EncodePayload()
    );
}

internal sealed class CastEffectAttachFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_016D_TBD,
        new Packet016DTbd
        {
            TargetEntityId = ctx.TargetEid,
            CasterEntityId = ctx.CasterEid,
        }.EncodePayload()
    );
}

internal sealed class CastEndTargetFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_002B_TBD,
        new Packet002BTbd
        {
            EntityId = ctx.TargetEid,
            EndFlag = Packet002BTbd.EndFlagContinuous,
        }.EncodePayload()
    );
}

internal sealed class CastTargetAckFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_0020_TBD,
        new Packet0020Tbd { EntityId = ctx.TargetEid }.EncodePayload()
    );
}

internal sealed class CastCounterFrameBuilder : ICastFrameBuilder
{
    private const byte CounterTogglePost = 0x01;

    private readonly byte _toggle;

    public CastCounterFrameBuilder(byte toggle)
    {
        _toggle = toggle;
    }

    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_011D_TBD,
        new Packet011DTbd
        {
            Toggle = _toggle,
            CombatSequence = _toggle == CounterTogglePost ? ctx.CastSequence + 2 : ctx.CastSequence,
        }.EncodePayload()
    );
}

internal sealed class CastTargetHpFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_011E_TBD,
        new Packet011ETbd
        {
            TargetEntityId = ctx.TargetEid,
            Damage = ctx.Damage,
            ZeroPad0407 = 0,
            Flag = 0,
        }.EncodePayload()
    );
}

internal sealed class CastSyncMarkerFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_05DD_TBD,
        Packet05DDTbd.EncodePayload()
    );
}

internal sealed class CastFinalHaltFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_0019_TBD,
        new HaltPacket
        {
            EntityId = ctx.CasterEid,
            X = ctx.CasterX,
            Y = ctx.CasterY,
            Flag = 0x02,
        }.EncodePayload()
    );
}

internal sealed class CastPostAnimCodeFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_0406_TBD,
        new Packet0406Tbd { PostAnimCode = Packet0406Tbd.MagicSoulDefault }.EncodePayload()
    );
}

internal sealed class MagicCastAnimTriggerFrameBuilder : ICastFrameBuilder
{
    private const ushort MagicSoulAnimSubOp = 0x001E;
    private const byte ResultFlagSuccess = 0x03;

    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_001F_TBD,
        new Packet001FTbd
        {
            CasterEntityId = ctx.CasterEid,
            TargetEntityId = ctx.TargetEid,
            CasterX = ctx.CasterX,
            CasterY = ctx.CasterY,
            TargetX = ctx.TargetX,
            TargetY = ctx.TargetY,
            AnimSubOp = MagicSoulAnimSubOp,
            ResultFlag = ResultFlagSuccess,
        }.EncodePayload()
    );
}

internal sealed class MagicCombatStanceFrameBuilder : ICastFrameBuilder
{
    public (Opcode, byte[]) Build(CastContext ctx) =>
    (
        Opcode.S2C_OP_001B_TBD,
        new Packet001BTbd
        {
            EntityId = ctx.CasterEid,
            WorldX = ctx.CasterX,
            WorldY = ctx.CasterY,
            StateFlag = Packet001BTbd.StateFlagCombatStance,
        }.EncodePayload()
    );
}
