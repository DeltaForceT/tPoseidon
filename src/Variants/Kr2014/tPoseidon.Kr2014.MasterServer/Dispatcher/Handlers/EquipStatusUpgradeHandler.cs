using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class EquipStatusUpgradeHandler : IInterServerHandler
{
    public static readonly InterServerOpcode[] Requests = { InterServerOpcode.TpEquipStatusUpgrade };

    private readonly IEquipStatusUpgradeStore _store;

    public EquipStatusUpgradeHandler(IEquipStatusUpgradeStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
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
            var upgrade = EquipStatusUpgradeMessages.DecodeRequest(frame.Body);
            var result = _store.Apply(upgrade);
            (opcode, body) = result.Succeeded
                ? (InterServerOpcode.TpEquipStatusUpgradeAckOk, EquipStatusUpgradeMessages.EncodeResult(result))
                : (InterServerOpcode.TpEquipStatusUpgradeAckCancel, EquipStatusUpgradeMessages.EncodeError(result.Error));
            ctx.Log(result.Succeeded
                ? $"equip status upgrade 0x{upgrade.CharacterId:X16}: {result.Items.Count} item rows, fee {upgrade.Fee}, money left {result.RemainMoney}"
                : $"equip status upgrade 0x{upgrade.CharacterId:X16}: refused with code {result.Error}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"equip status upgrade char 0x{charUid:X16}: store threw {ex.GetType().Name}: {ex.Message}");
            (opcode, body) = (InterServerOpcode.TpEquipStatusUpgradeAckCancel, EquipStatusUpgradeMessages.EncodeError(EquipStatusUpgradeRules.StoreFailed));
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
}
