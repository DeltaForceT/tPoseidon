using System;
using System.Threading;
using System.Threading.Tasks;
using tPoseidon.Core.Common;
using tPoseidon.Core.Game.Combat;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Combat;

public delegate Task SendCastFrame(Opcode opcode, byte[] payload);

public sealed class CastBroadcaster
{
    private const int PlaceholderAp = 50;
    private const int PlaceholderMa = 50;
    private const int PlaceholderSa = 50;
    private const int PlaceholderCa = 50;
    private const int PlaceholderDefenderDp = 0;
    private const int PlaceholderSkillLevel = 1;

    private readonly ISkillCatalog _skills;
    private readonly MonsterManager _monsters;
    private readonly TransientEntityIdAllocator _transientEids;
    private readonly SkillCastResolver _castResolver;

    public CastBroadcaster(
        ISkillCatalog skills,
        MonsterManager monsters,
        TransientEntityIdAllocator transientEids,
        SkillCastResolver castResolver)
    {
        _skills = skills;
        _monsters = monsters;
        _transientEids = transientEids;
        _castResolver = castResolver;
    }

    public async Task BroadcastPlayerCastAsync(
        ushort casterEid,
        ushort casterX,
        ushort casterY,
        ushort targetEid,
        ushort skillId,
        byte slot,
        uint castSequence,
        SendCastFrame send,
        CancellationToken ct)
    {
        if (casterEid == 0) return;

        var skill = _skills.GetById(skillId);
        if (skill is null) return;
        if (!skill.IsActive) return;

        var mob = _monsters.Get(targetEid);
        if (mob is null || mob.IsDead) return;

        var (targetX, targetY) = mob.GetInterpolatedPosition(DateTimeOffset.UtcNow);

        var projectileEid = (ushort)(casterEid + 2 * castSequence - 1);
        var castDelayMs = _castResolver.GetCastDelayMs(skill, PlaceholderSkillLevel);
        var attackerStat = AttackerStatForClass(skill.SkillClass);
        var damage = _castResolver.ComputeDamage(skill, PlaceholderSkillLevel, attackerStat, PlaceholderDefenderDp);

        var ctx = new CastContext(
            CasterEid: casterEid,
            CasterX: casterX,
            CasterY: casterY,
            TargetEid: targetEid,
            TargetX: targetX,
            TargetY: targetY,
            Skill: skill,
            CastSequence: castSequence,
            Slot: slot,
            Damage: 0,
            ProjectileEid: projectileEid);

        Console.WriteLine($"[CAST_TRACE] skill_id=0x{skillId:X4} caster_eid=0x{casterEid:X4} target_eid=0x{targetEid:X4} projectile_eid=0x{projectileEid:X4} damage={damage} target_xy=({targetX},{targetY}) caster_xy=({casterX},{casterY})");

        var castInit = CastChainSelector.InitChain(skill);
        foreach (var builder in castInit)
        {
            var (opcode, body) = builder.Build(ctx);
            Console.WriteLine($"[CAST_TRACE] init  op=0x{(ushort)opcode:X4} len={body.Length} hex={Convert.ToHexString(body)}");
            await send(opcode, body).ConfigureAwait(false);
        }

        try
        {
            await Task.Delay(castDelayMs, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        _monsters.ApplyDamage(targetEid, casterEid, damage);

        var resolvedCtx = ctx with { Damage = (ushort)damage };

        var castEnd = CastChainSelector.CastEndChain(skill);
        foreach (var builder in castEnd)
        {
            var (opcode, body) = builder.Build(resolvedCtx);
            Console.WriteLine($"[CAST_TRACE] end   op=0x{(ushort)opcode:X4} len={body.Length} hex={Convert.ToHexString(body)}");
            await send(opcode, body).ConfigureAwait(false);
        }

        var resolve = CastChainSelector.ResolveChain(skill);
        foreach (var builder in resolve)
        {
            var (opcode, body) = builder.Build(resolvedCtx);
            Console.WriteLine($"[CAST_TRACE] resv  op=0x{(ushort)opcode:X4} len={body.Length} hex={Convert.ToHexString(body)}");
            await send(opcode, body).ConfigureAwait(false);
        }
    }

    private static int AttackerStatForClass(SkillClass skillClass) => skillClass switch
    {
        SkillClass.Magic => PlaceholderMa,
        SkillClass.Power => PlaceholderAp,
        SkillClass.Sense => PlaceholderSa,
        SkillClass.Charm => PlaceholderCa,
        _ => PlaceholderAp,
    };
}

public sealed record CastContext(
    ushort CasterEid,
    ushort CasterX,
    ushort CasterY,
    ushort TargetEid,
    ushort TargetX,
    ushort TargetY,
    ISkill Skill,
    uint CastSequence,
    byte Slot,
    ushort Damage,
    ushort ProjectileEid);

public interface ICastFrameBuilder
{
    (Opcode Opcode, byte[] Body) Build(CastContext ctx);
}

public static class CastChainSelector
{
    private const byte CounterTogglePre = 0x00;
    private const byte CounterTogglePost = 0x01;

    private static readonly ICastFrameBuilder[] MagicInitChain =
    {
        new CastInitFrameBuilder(),
        new CastHaltFrameBuilder(),
    };

    private static readonly ICastFrameBuilder[] MagicCastEndChain =
    {
        new CastEndFrameBuilder(),
        new CastEffectAttachFrameBuilder(),
    };

    private static readonly ICastFrameBuilder[] MagicResolveChain =
    {
        new CastConfirmFrameBuilder(),
        new CastHitTargetFrameBuilder(),
        new CastEndTargetFrameBuilder(),
        new CastTargetAckFrameBuilder(),
        new CastCounterFrameBuilder(CounterTogglePre),
        new CastProjectileSpawnFrameBuilder(),
        new CastTargetHpFrameBuilder(),
        new CastPostAnimCodeFrameBuilder(),
        new CastSyncMarkerFrameBuilder(),
        new CastCounterFrameBuilder(CounterTogglePost),
        new CastFinalHaltFrameBuilder(),
    };

    public static ICastFrameBuilder[] InitChain(ISkill skill) => skill.SkillClass switch
    {
        SkillClass.Magic => MagicInitChain,
        _ => MagicInitChain,
    };

    public static ICastFrameBuilder[] CastEndChain(ISkill skill) => skill.SkillClass switch
    {
        SkillClass.Magic => MagicCastEndChain,
        _ => MagicCastEndChain,
    };

    public static ICastFrameBuilder[] ResolveChain(ISkill skill) => skill.SkillClass switch
    {
        SkillClass.Magic => MagicResolveChain,
        _ => MagicResolveChain,
    };
}
