using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterGuardian
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterGuardian(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<GuardianLogin?> LoadAsync(ulong charUid, CancellationToken ct)
    {
        var reply = await CallAsync(charUid, InterServerOpcode.TpGuardianLoad, InterServerOpcode.TpGuardianLoadAckOk,
            InterServerOpcode.TpGuardianLoadAckCancel, [], ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? GuardianMessages.DecodeLogin(answer.Body) : null;
    }

    public async Task<GuardianState?> EquipAsync(ulong charUid, ulong guardianUid, CancellationToken ct)
    {
        var reply = await CallAsync(charUid, InterServerOpcode.CcmdGuardianEquip, InterServerOpcode.CcmdGuardianEquipAckOk,
            InterServerOpcode.CcmdGuardianEquipAckCancel, GuardianMessages.EncodeUid(guardianUid), ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? GuardianMessages.DecodeState(answer.Body) : null;
    }

    public async Task<bool> OrderAsync(ulong charUid, GuardianOrderChange change, CancellationToken ct)
    {
        var reply = await CallAsync(charUid, InterServerOpcode.CcmdGuardianOrder, InterServerOpcode.CcmdGuardianOrderAckOk,
            InterServerOpcode.CcmdGuardianOrderAckCancel, GuardianMessages.EncodeOrder(change), ct).ConfigureAwait(false);
        return reply is { Ok: true };
    }

    public async Task<EquipStatusUpgradeResult> SaveCardsAsync(ulong charUid, GuardianCardSave save, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(save);
        var reply = await CallAsync(charUid, InterServerOpcode.CcmdGuardianCards, InterServerOpcode.CcmdGuardianCardsAckOk,
            InterServerOpcode.CcmdGuardianCardsAckCancel, GuardianMessages.EncodeCardSave(save), ct).ConfigureAwait(false);
        return reply switch
        {
            { Ok: true } answer => EquipStatusUpgradeMessages.DecodeResult(answer.Body),
            { Ok: false } refused => EquipStatusUpgradeResult.Refused(EquipStatusUpgradeMessages.DecodeError(refused.Body)),
            null => EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.StoreFailed),
        };
    }

    public async Task<EquipStatusUpgradeResult> RenameAsync(ulong charUid, GuardianRename rename, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rename);
        var reply = await CallAsync(charUid, InterServerOpcode.IcmdGuardianRename, InterServerOpcode.IcmdGuardianRenameAckOk,
            InterServerOpcode.IcmdGuardianRenameAckCancel, GuardianMessages.EncodeRename(rename), ct).ConfigureAwait(false);
        return reply switch
        {
            { Ok: true } answer => EquipStatusUpgradeMessages.DecodeResult(answer.Body),
            { Ok: false } refused => EquipStatusUpgradeResult.Refused(EquipStatusUpgradeMessages.DecodeError(refused.Body)),
            null => EquipStatusUpgradeResult.Refused(EquipStatusUpgradeRules.StoreFailed),
        };
    }

    private async Task<(bool Ok, byte[] Body)?> CallAsync(ulong charUid, InterServerOpcode request, InterServerOpcode ok, InterServerOpcode cancel, byte[] body, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[guardian] Master not connected; 0x{(ushort)request:X4} for 0x{charUid:X} not sent");
            return null;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(new InterServerFrame
            {
                Opcode = request,
                Header = new InterServerHeader
                {
                    SourceServerIndex = _selfIndex,
                    DestServerIndex = 0,
                    CharUid = charUid,
                    RequestId = client.AllocateRequestId(),
                },
                Body = body,
            }, new HashSet<InterServerOpcode> { ok, cancel }, Timeout, ct).ConfigureAwait(false);
            return (reply.Opcode == ok, reply.Body);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[guardian] Master did not answer 0x{(ushort)request:X4} for 0x{charUid:X}");
            return null;
        }
    }
}
