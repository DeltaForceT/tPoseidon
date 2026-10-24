using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterEquipStatusUpgrade
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterEquipStatusUpgrade(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<EquipStatusUpgradeResult> ApplyAsync(EquipStatusUpgrade upgrade, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(upgrade);
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[equip-upgrade] Master not connected; upgrade for 0x{upgrade.CharacterId:X} not sent");
            return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.StoreFailed);
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = InterServerOpcode.TpEquipStatusUpgrade,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = upgrade.CharacterId,
                    RequestId = client.AllocateRequestId(),
                },
                Body = EquipStatusUpgradeMessages.EncodeRequest(upgrade),
            }, new HashSet<InterServerOpcode> { InterServerOpcode.TpEquipStatusUpgradeAckOk, InterServerOpcode.TpEquipStatusUpgradeAckCancel }, Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == InterServerOpcode.TpEquipStatusUpgradeAckOk
                ? EquipStatusUpgradeMessages.DecodeResult(reply.Body)
                : EquipStatusUpgradeResult.Refused(EquipStatusUpgradeMessages.DecodeError(reply.Body));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[equip-upgrade] Master did not answer the upgrade for 0x{upgrade.CharacterId:X}");
            return EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.StoreFailed);
        }
    }
}
