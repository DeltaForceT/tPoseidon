using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class SoulSeedHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests =
    {
        InterServerOpcode.TpSoulSeedLoad,
        InterServerOpcode.McmdSoulSeedUse,
        InterServerOpcode.McmdSoulSeedUpdate,
        InterServerOpcode.CcmdGuardianAwaken,
        InterServerOpcode.CcmdGuardianAwakenReset,
    };

    private readonly ISoulSeedStateStore _seeds;
    private readonly IGuardianStateStore _guardians;

    public SoulSeedHandler(ISoulSeedStateStore seeds, IGuardianStateStore guardians)
    {
        _seeds = seeds ?? throw new ArgumentNullException(nameof(seeds));
        _guardians = guardians ?? throw new ArgumentNullException(nameof(guardians));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        (InterServerOpcode Opcode, byte[] Body)? reply;
        try
        {
            reply = frame.Opcode switch
            {
                InterServerOpcode.TpSoulSeedLoad => (InterServerOpcode.TpSoulSeedLoadAckOk, SoulSeedMessages.EncodeState(_seeds.LoadActive(charUid))),
                InterServerOpcode.McmdSoulSeedUse => Use(charUid, SoulSeedMessages.DecodeUse(frame.Body), ctx),
                InterServerOpcode.McmdSoulSeedUpdate => Update(charUid, SoulSeedMessages.DecodePoints(frame.Body), ctx),
                InterServerOpcode.CcmdGuardianAwakenReset => Reset(charUid, SoulSeedMessages.DecodeReset(frame.Body), ctx),
                _ => Awaken(SoulSeedMessages.DecodeAwaken(charUid, frame.Body), ctx),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"soul seed 0x{(ushort)frame.Opcode:X4} char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            reply = frame.Opcode switch
            {
                InterServerOpcode.TpSoulSeedLoad => (InterServerOpcode.TpSoulSeedLoadAckCancel, []),
                InterServerOpcode.McmdSoulSeedUse => (InterServerOpcode.McmdSoulSeedUseAckCancel, []),
                InterServerOpcode.CcmdGuardianAwaken => (InterServerOpcode.CcmdGuardianAwakenAckCancel, []),
                InterServerOpcode.CcmdGuardianAwakenReset => (InterServerOpcode.CcmdGuardianAwakenAckCancel, []),
                _ => null,
            };
        }
        if (reply is not { } answer) return;
        await session.SendAsync(new InterServerFrame
        {
            Opcode = answer.Opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = charUid,
                RequestId = frame.Header.RequestId,
            },
            Body = answer.Body,
        }, ct).ConfigureAwait(false);
    }

    private (InterServerOpcode, byte[]) Use(ulong charUid, SoulSeedUseRequest request, MasterContext ctx)
    {
        var error = _seeds.Use(charUid, request.SeedItemIndex);
        ctx.Log($"soul seed use char 0x{charUid:X16} seed {request.SeedItemIndex}: {error}");
        return error == SoulSeedUseError.None
            ? (InterServerOpcode.McmdSoulSeedUseAckOk, SoulSeedMessages.EncodeUid(request.ItemUid))
            : (InterServerOpcode.McmdSoulSeedUseAckCancel, SoulSeedMessages.EncodeCode((uint)error));
    }

    private (InterServerOpcode, byte[])? Update(ulong charUid, IReadOnlyList<short> points, MasterContext ctx)
    {
        if (!_seeds.SavePoints(charUid, points)) ctx.Log($"soul seed update char 0x{charUid:X16}: no seed row");
        return null;
    }

    private (InterServerOpcode, byte[]) Reset(ulong charUid, ushort skillId, MasterContext ctx)
    {
        _guardians.ResetAwakening(charUid, skillId);
        ctx.Log($"CCMD_AWAKEN_RESET_ACK_OK:chrUid{charUid} (skill {skillId})");
        return (InterServerOpcode.CcmdGuardianAwakenResetAckOk, []);
    }

    private (InterServerOpcode, byte[]) Awaken(GuardianAwakening awakening, MasterContext ctx)
    {
        var result = _guardians.Awaken(awakening);
        ctx.Log($"awaken char 0x{awakening.CharacterId:X16} type {awakening.GuardianType} rare {awakening.RareType}: {result.Error}");
        return result.Succeeded
            ? (InterServerOpcode.CcmdGuardianAwakenAckOk, SoulSeedMessages.EncodeAwakened(new GuardianAwakened(result.GuardianUid, result.SlotNum)))
            : (InterServerOpcode.CcmdGuardianAwakenAckCancel, SoulSeedMessages.EncodeCode((uint)result.Error));
    }
}
