using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SkillFramesTests
{
    private const ushort BlockId = 9;
    private const ushort Caster = 0x0301;
    private const ushort Target = 0x0201;
    private const ushort PunchSkill = 1012;

    private static SkillDefinition Punch() => new(
        new SkillParam2Entry
        {
            Id = PunchSkill,
            Type = 2,
            MaxSkillLevel = 1,
            EsAction = SkillActions.Punch,
            EsClass = -1,
            EState = 0,
            TargetInput = SkillDefinition.ObjectTargetInput,
        },
        Array.Empty<SkillTableRow>(),
        Array.Empty<SkillTableRow>());

    private static PlayerStrike Strike(int damage, int hpAfter, bool killed) => new(
        AttackerEntityId: Caster,
        TargetEntityId: Target,
        AttackerX: 328,
        AttackerY: 232,
        TargetX: 344,
        TargetY: 248,
        Damage: damage,
        MobHpAfter: hpAfter,
        HitFlags: 0,
        HitKind: killed ? (uint)0x80 : 0,
        Killed: killed,
        NextSwingAtUtc: default,
        MonsterParamId: 2100,
        MobMaxHp: 240);

    [Fact]
    public void AUseRequestIsTheSkillAndItsLevel()
    {
        Assert.Equal(new SkillRequest(PunchSkill, 1), SkillFrames.ReadUse(new byte[] { 0xF4, 0x03, 0x01 }));
        Assert.Null(SkillFrames.ReadUse(new byte[] { 0xF4, 0x03 }));
    }

    [Fact]
    public void ADroppedSelectionStartsWithItsKind()
    {
        Assert.Equal(SkillFrames.SkillKind, SkillFrames.ReadSelectionDroppedKind(new byte[] { 0x02, 0xC7, 0x0B, 0x01 }));
        Assert.Null(SkillFrames.ReadSelectionDroppedKind(new byte[] { 0x02, 0xC7, 0x0B }));
    }

    [Fact]
    public void ATargetRequestCarriesTheKindTheSkillTheTargetTypeAndItsData()
    {
        var request = SkillFrames.ReadTarget(new byte[] { 0x02, 0xF4, 0x03, 0x01, 0x01, 0x00, 0x02, 0x00, 0x01, 0x02 });

        Assert.NotNull(request);
        Assert.Equal(
            (SkillFrames.SkillKind, PunchSkill, (byte)1, SkillFrames.ObjectTargetType, (ushort?)Target),
            (request.Value.Kind, request.Value.SkillId, request.Value.Level, request.Value.TargetType, request.Value.TargetEntityId));
        Assert.Null(SkillFrames.ReadTarget(new byte[] { 0x02, 0xF4, 0x03, 0x01, 0x01, 0x00, 0x02 }));
        Assert.Null(SkillFrames.ReadTarget(new byte[] { 0x02, 0xF4, 0x03, 0x01, 0x01, 0x00, 0x00, 0x00 })!.Value.TargetEntityId);
    }

    [Fact]
    public void TheShortRepliesFollowTheirLayouts()
    {
        Assert.Equal(new byte[] { 0x06, 0x00 }, SkillFrames.Refused(SkillFailure.NotEnoughMp));
        Assert.Equal(new byte[] { 0x0F, 0x00, 0x00, 0x00, 0x00 }, SkillFrames.Notice(SkillFailure.WrongPlaceNotice, 0, 0));
        Assert.Equal(new byte[] { 0x02, 0xF4, 0x03, 0x01 }, SkillFrames.TargetAsked(PunchSkill, 1));
        Assert.Equal(new byte[] { 0xF4, 0x03, 0x01 }, SkillFrames.Completed(PunchSkill, 1));
        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x02, 0xF4, 0x03, 0x01, 0x01, 0x01, 0x02 },
            SkillFrames.CastStarted(Caster, PunchSkill, 1, Target));
    }

    [Fact]
    public void TheEffectHeaderNamesBothEntitiesTheActionAndTheSkill()
    {
        var header = SkillFrames.EffectHeader(Caster, Target, SkillActions.Punch, -1, PunchSkill, 0);

        Assert.Equal(SkillFrames.EffectHeaderSize, header.Length);
        Assert.Equal(
            new byte[] { 0x01, 0x03, 0x01, 0x02, 0x00, 0x00, 0x10, 0x05, 0x02, 0xFF, 0xF4, 0x03, 0x00, 0x00 },
            header);
    }

    [Fact]
    public void TheHitResultCarriesDamageTypeKindAndElement()
    {
        var payload = SkillFrames.HitResult(
            Target, new SkillHit(185, SkillDamage.PhysicalType, SkillDamage.KindFinishingFlag, SkillActions.NoPropertyElement, 0));

        Assert.Equal(SkillFrames.HitResultSize, payload.Length);
        Assert.Equal(
            new byte[]
            {
                0x01, 0x02,
                0xB9, 0x00, 0x00, 0x00,
                0x02, 0x00, 0x00, 0x00,
                0x80, 0x00, 0x00, 0x00,
                0x80, 0x00, 0x00, 0x00,
            },
            payload[..18]);
        Assert.Equal(new byte[12], payload[18..]);
    }

    [Fact]
    public void AHitIsOneBlockThatOpensWithTheEffect()
    {
        var hit = new SkillHit(60, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0);
        var additions = new SwingAdditions(
            null,
            Array.Empty<(Opcode Opcode, byte[] Payload)>(),
            new[] { (Opcode.S2C_OP_0406_TBD, new byte[4], SwingAudience.Attacker) });

        var frames = SkillFrames.EncodeHit(
            Strike(60, 180, killed: false), Punch(), hit, BlockId, additions,
            new[] { (Opcode.S2C_OP_011D_TBD, new byte[5]) });

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_00E0_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_002A_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_011E_TBD, BlockId, SwingAudience.Engaged),
                (Opcode.S2C_OP_0406_TBD, BlockId, SwingAudience.Attacker),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_011D_TBD, MeleeSwingFrames.NoBlock, SwingAudience.Attacker),
            },
            frames.Select(f => (f.Opcode, f.BlockId, f.Audience)).ToArray());
    }

    [Fact]
    public void AJobHitNamesTheCasterTwiceAndAddsTargetAndPositions()
    {
        var hit = new SkillHit(60, SkillDamage.PhysicalType, SkillDamage.KindHit, 0, 0);
        var additions = new SwingAdditions(
            null,
            Array.Empty<(Opcode Opcode, byte[] Payload)>(),
            Array.Empty<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>());

        var effect = SkillFrames.EncodeHit(
            Strike(60, 180, killed: false), Punch(), hit, BlockId, additions,
            Array.Empty<(Opcode Opcode, byte[] Payload)>(), casterTwice: true, tail: SkillFrames.PositionTail(Strike(60, 180, killed: false)))[0];

        Assert.Equal(Opcode.S2C_OP_00E0_TBD, effect.Opcode);
        Assert.Equal(
            new byte[]
            {
                0x01, 0x03, 0x01, 0x03, 0x00, 0x00, 0x10, 0x05, 0x02, 0xFF, 0xF4, 0x03, 0x00, 0x00,
                0x01, 0x02, 0x48, 0x01, 0xE8, 0x00, 0x58, 0x01, 0xF8, 0x00,
            },
            effect.Payload);
    }

    [Fact]
    public void AKillPutsTheOwnerMarkBeforeTheBlockAndTheDeathRewardsAndDropsInside()
    {
        var hit = new SkillHit(240, SkillDamage.PhysicalType, SkillDamage.KindFinishingFlag, 0, 0);
        var additions = new SwingAdditions(
            new byte[] { 0x01, 0x02, 0x01, 0x03 },
            new[] { (Opcode.S2C_OP_011D_TBD, new byte[5]) },
            new[] { (Opcode.S2C_OP_0406_TBD, new byte[4], SwingAudience.Attacker) },
            new[] { (Opcode.S2C_OP_000C_TBD, new byte[28]) });

        var frames = SkillFrames.EncodeHit(
            Strike(240, 0, killed: true), Punch(), hit, BlockId, additions,
            Array.Empty<(Opcode Opcode, byte[] Payload)>());

        Assert.Equal(
            new[]
            {
                (Opcode.S2C_OP_016D_TBD, MeleeSwingFrames.NoBlock, SwingAudience.Everyone),
                (Opcode.S2C_OP_00E0_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_002A_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_002B_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_011D_TBD, BlockId, SwingAudience.Owner),
                (Opcode.S2C_OP_000C_TBD, BlockId, SwingAudience.Everyone),
                (Opcode.S2C_OP_011E_TBD, BlockId, SwingAudience.Engaged),
                (Opcode.S2C_OP_0406_TBD, BlockId, SwingAudience.Attacker),
                (Opcode.S2C_COMMAND_BLOCK_END, BlockId, SwingAudience.Everyone),
            },
            frames.Select(f => (f.Opcode, f.BlockId, f.Audience)).ToArray());
    }

    [Fact]
    public void ASkillEffectNeedsABlock()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SkillFrames.EncodeHit(
            Strike(1, 1, killed: false), Punch(), default, MeleeSwingFrames.NoBlock, SwingAdditions.None,
            Array.Empty<(Opcode Opcode, byte[] Payload)>()));
    }

    [Fact]
    public void ASkillRecordIsIdLevelAndPoints()
    {
        var bytes = new byte[] { 0xE9, 0x03, 0x02, 0x01, 0x00 };

        Assert.True(SkillFrames.TryReadRecord(bytes, out var skillId, out var level, out var points));
        Assert.Equal((1001, 2, 1), (skillId, level, points));
        Assert.Equal(bytes, SkillFrames.Record(1001, 2, 1));
        Assert.False(SkillFrames.TryReadRecord(bytes.AsSpan(0, 4), out _, out _, out _));
    }

    [Fact]
    public void TheLevelSeenFrameNamesPlayerTypeAndLevel()
    {
        Assert.Equal(new byte[] { 0x7E, 0x2E, 0x02, 0x00, 0x05, 0x00 }, SkillFrames.LevelSeen(0x2E7E, 2, 5));
    }

    [Fact]
    public void TornadoBombCarriesThreeSlotsAndAFlaggedTotal()
    {
        var outcome = new TornadoOutcome(new (long, int, ushort)[] { (30, 0, 0xFFFF), (0, 1, 0xFFFF), (40, 3, 0xFFFF) }, 70, 2, 0, false);
        var additions = new SwingAdditions(
            null,
            Array.Empty<(Opcode Opcode, byte[] Payload)>(),
            Array.Empty<(Opcode Opcode, byte[] Payload, SwingAudience Audience)>());

        var frames = SkillFrames.EncodeTornado(
            Strike(70, 170, killed: false), Punch(), outcome, BlockId, additions, Array.Empty<(Opcode Opcode, byte[] Payload)>());

        Assert.Equal(SkillFrames.EffectHeaderSize + SkillFrames.TornadoTailSize, frames[0].Payload.Length);
        Assert.Equal(new byte[] { 0x48, 0x01, 0xE8, 0x00, 0x01, 0x02, 0x58, 0x01, 0xF8, 0x00, 0x1E, 0x00, 0x00, 0x00 }, frames[0].Payload[14..28]);
        Assert.Equal(new byte[] { 0xFF, 0xFF }, frames[0].Payload[^2..]);
        var result = frames[1].Payload;
        Assert.Equal(Opcode.S2C_OP_002A_TBD, frames[1].Opcode);
        Assert.Equal(new byte[] { 0x46, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00 }, result[2..10]);
        Assert.Equal(new byte[] { 0x00, 0x80, 0x00, 0x00 }, result[18..22]);
        Assert.Equal(new byte[] { 0x02, 0x00 }, result[26..28]);
    }

    [Fact]
    public void CureNamesTheTargetBothPositionsAndWhetherItHealed()
    {
        var frames = SkillFrames.EncodeHeal(Caster, (328, 232), Target, (344, 248), healed: true, Punch(), BlockId,
            Array.Empty<(Opcode Opcode, byte[] Payload)>());

        Assert.Equal(new byte[] { 0x01, 0x02, 0x48, 0x01, 0xE8, 0x00, 0x58, 0x01, 0xF8, 0x00, 0x01 }, frames[0].Payload[14..]);
        Assert.Equal(Opcode.S2C_COMMAND_BLOCK_END, frames[1].Opcode);
    }
}
