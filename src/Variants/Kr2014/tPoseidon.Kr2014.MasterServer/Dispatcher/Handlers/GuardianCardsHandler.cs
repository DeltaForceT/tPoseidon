using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class GuardianCardsHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests = { InterServerOpcode.CcmdGuardianCards };

    private readonly IGuardianStateStore _guardians;
    private readonly IEquipStatusUpgradeStore _items;

    public GuardianCardsHandler(IGuardianStateStore guardians, IEquipStatusUpgradeStore items)
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
            (opcode, body) = Save(charUid, GuardianMessages.DecodeCardSave(frame.Body), ctx);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"guardian cards char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = (InterServerOpcode.CcmdGuardianCardsAckCancel, EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.StoreFailed));
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

    private (InterServerOpcode, byte[]) Save(ulong charUid, GuardianCardSave save, MasterContext ctx)
    {
        if (save.Items.CharacterId != charUid || _guardians.Load(charUid, save.GuardianUid) is null)
        {
            ctx.Log($"guardian cards char 0x{charUid:X16} guardian {save.GuardianUid}: not the character's guardian");
            return (InterServerOpcode.CcmdGuardianCardsAckCancel, EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.StoreFailed));
        }
        var taken = _items.Apply(save.Items);
        if (!taken.Succeeded)
        {
            ctx.Log($"guardian cards char 0x{charUid:X16} guardian {save.GuardianUid}: item operation refused with code {taken.Error}");
            return (InterServerOpcode.CcmdGuardianCardsAckCancel, EquipStatusUpgradeMessages.EncodeError(taken.Error));
        }
        if (!_guardians.SaveCards(save.GuardianUid, save.SkillPoint, save.UpdateCount, save.Cards))
        {
            ctx.Log($"guardian cards char 0x{charUid:X16} guardian {save.GuardianUid}: slot update failed after the item operations");
            return (InterServerOpcode.CcmdGuardianCardsAckCancel, EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.StoreFailed));
        }
        ctx.Log($"guardian cards char 0x{charUid:X16} guardian {save.GuardianUid}: {save.Cards.Count} of {save.UpdateCount} cards, {taken.Items.Count} item operations");
        return (InterServerOpcode.CcmdGuardianCardsAckOk, EquipStatusUpgradeMessages.EncodeResult(taken));
    }
}
