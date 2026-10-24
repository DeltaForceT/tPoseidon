using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterFortune
{
    private readonly MasterChannel _channel;

    public MasterFortune(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _channel = new MasterChannel(client, selfIndex, "fortune", log);
    }

    public async Task<(bool Loaded, FortuneInfo? Info)> LoadAsync(ulong charUid, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.TpFortuneLoad, InterServerOpcode.TpFortuneLoadAckOk,
            InterServerOpcode.TpFortuneLoadAckCancel, [], ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? (true, FortuneMessages.DecodeInfo(charUid, answer.Body)) : (false, null);
    }

    public Task UpdateAsync(FortuneInfo info, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(info);
        return _channel.SendAsync(info.CharacterId, InterServerOpcode.McmdFortuneUpdate, FortuneMessages.EncodeInfo(info), ct);
    }
}
