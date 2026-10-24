using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class GuardianRenameHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests = { InterServerOpcode.IcmdGuardianRename };

    private readonly IGuardianStateStore _guardians;
    private readonly IEquipStatusUpgradeStore _items;

    public GuardianRenameHandler(IGuardianStateStore guardians, IEquipStatusUpgradeStore items)
    {
        _guardians = guardians ?? throw new ArgumentNullException(nameof(guardians));
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(ctx);
        var charUid = frame.Header.CharUid;
        InterServerOpcode opcode;
        byte[] body;
        try
        {
            (opcode, body) = Rename(charUid, GuardianMessages.DecodeRename(frame.Body), ctx);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guardian rename char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = (InterServerOpcode.IcmdGuardianRenameAckCancel, EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.StoreFailed));
        }
        await session.SendAsync(new InterServerFrame
        {
            Opcode = opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = frame.Header.SourceServerIndex,
                CharUid = charUid,
                RequestId = frame.Header.RequestId,
            },
            Body = body,
        }, ct).ConfigureAwait(false);
    }

    private (InterServerOpcode, byte[]) Rename(ulong charUid, GuardianRename rename, MasterContext ctx)
    {
        if (rename.Item.CharacterId != charUid || _guardians.Load(charUid, rename.GuardianUid) is null)
        {
            ctx.Log($"guardian rename char 0x{charUid:X16} guardian {rename.GuardianUid}: not the character's guardian");
            return (InterServerOpcode.IcmdGuardianRenameAckCancel, EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.StoreFailed));
        }
        if (!_guardians.Rename(rename.GuardianUid, rename.Name))
        {
            ctx.Log($"guardian rename char 0x{charUid:X16} guardian {rename.GuardianUid}: name not stored");
            return (InterServerOpcode.IcmdGuardianRenameAckCancel, EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.StoreFailed));
        }
        var taken = _items.Apply(rename.Item);
        if (!taken.Succeeded)
        {
            ctx.Log($"guardian rename char 0x{charUid:X16} guardian {rename.GuardianUid}: renamed, item refused with code {taken.Error}");
            return (InterServerOpcode.IcmdGuardianRenameAckCancel, EquipStatusUpgradeMessages.EncodeError(taken.Error));
        }
        ctx.Log($"guardian rename char 0x{charUid:X16} guardian {rename.GuardianUid}: '{rename.Name}'");
        return (InterServerOpcode.IcmdGuardianRenameAckOk, EquipStatusUpgradeMessages.EncodeResult(taken));
    }
}
