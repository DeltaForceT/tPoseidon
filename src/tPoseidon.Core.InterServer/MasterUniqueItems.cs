using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterUniqueItems
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private readonly Func<MasterServerClient?> _client;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;

    public MasterUniqueItems(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _selfIndex = selfIndex;
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task PickedUpAsync(ulong charUid, ulong itemUid, int itemIndex, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[unique] Master not connected; pickup of item {itemIndex} by 0x{charUid:X} not recorded");
            return;
        }
        await client.SendAsync(Frame(client, InterServerOpcode.TpUniqueItemPickup, charUid, UniqueItemMessages.EncodePickup(itemUid, itemIndex)), ct).ConfigureAwait(false);
    }

    public async Task AddCountAsync(ulong charUid, uint key, uint units, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[unique] Master not connected; {units} units for key {key} not counted");
            return;
        }
        await client.SendAsync(Frame(client, InterServerOpcode.McmdUniqueItemCount, charUid, UniqueItemMessages.EncodeCount(key, units)), ct).ConfigureAwait(false);
    }

    public async Task BossKilledAsync(ulong charUid, ushort monsterId, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[unique] Master not connected; boss kill {monsterId} by 0x{charUid:X} not recorded");
            return;
        }
        await client.SendAsync(Frame(client, InterServerOpcode.CcmdSaveBossKillInfo, charUid, UniqueItemMessages.EncodeBossKill(charUid, monsterId)), ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BossKillRecord>?> LatestBossKillsAsync(ulong charUid, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[unique] Master not connected; boss kills of 0x{charUid:X} not read");
            return null;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(
                Frame(client, InterServerOpcode.CcmdGetLatestBossKill, charUid, Array.Empty<byte>()),
                new HashSet<InterServerOpcode> { InterServerOpcode.CcmdGetLatestBossKillAckOk, InterServerOpcode.CcmdGetLatestBossKillAckCancel },
                Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == InterServerOpcode.CcmdGetLatestBossKillAckOk ? UniqueItemMessages.DecodeLatestBossKills(reply.Body) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[unique] Master did not answer the boss kills of 0x{charUid:X}");
            return null;
        }
    }

    public async Task<IReadOnlyList<UniqueHeldItem>?> WithdrawAsync(UniqueWithdrawRequest request, CancellationToken ct)
    {
        if (_client() is not { IsConnected: true } client)
        {
            _log($"[unique] Master not connected; withdraw for 0x{request.CharUid:X} not sent");
            return null;
        }
        try
        {
            var reply = await client.SendAndAwaitReplyAsync(
                Frame(client, InterServerOpcode.IcmdUniqueItemWithdraw, request.CharUid, UniqueItemMessages.EncodeWithdraw(request)),
                new HashSet<InterServerOpcode> { InterServerOpcode.IcmdUniqueItemWithdrawAckOk, InterServerOpcode.IcmdUniqueItemWithdrawAckCancel },
                Timeout, ct).ConfigureAwait(false);
            return reply.Opcode == InterServerOpcode.IcmdUniqueItemWithdrawAckOk ? UniqueItemMessages.DecodeWithdrawn(reply.Body) : null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _log($"[unique] Master did not answer the withdraw for 0x{request.CharUid:X}");
            return null;
        }
    }

    private InterServerFrame Frame(MasterServerClient client, InterServerOpcode opcode, ulong charUid, byte[] body) => new()
    {
        Opcode = opcode,
        Header = new InterServerHeader
        {
            SourceServerIndex = _selfIndex,
            DestServerIndex = 0,
            CharUid = charUid,
            RequestId = client.AllocateRequestId(),
        },
        Body = body,
    };
}
