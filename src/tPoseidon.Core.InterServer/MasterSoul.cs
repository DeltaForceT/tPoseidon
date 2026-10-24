using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterSoul
{
    private readonly MasterChannel _channel;

    public MasterSoul(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _channel = new MasterChannel(client, selfIndex, "soul", log);
    }

    public async Task<SoulAccount?> LoadAsync(ulong charUid, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.TpSoulLoad, InterServerOpcode.TpSoulLoadAckOk,
            InterServerOpcode.TpSoulLoadAckCancel, [], ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? SoulMessages.DecodeAccount(answer.Body) : null;
    }

    public Task SaveSoulAsync(ulong charUid, uint soul, CancellationToken ct) =>
        _channel.SendAsync(charUid, InterServerOpcode.TpSoulSave, SoulMessages.EncodeSoul(soul), ct);

    public Task SaveGuardianAsync(ulong charUid, GuardianSoulRow row, CancellationToken ct) =>
        _channel.SendAsync(charUid, InterServerOpcode.TpSoulGuardianSave, SoulMessages.EncodeGuardian(row), ct);
}
