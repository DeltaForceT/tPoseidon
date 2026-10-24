using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public readonly record struct SoulSeedUseResult(bool Ok, uint Code);

public sealed class MasterSoulSeeds
{
    private readonly MasterChannel _channel;

    public MasterSoulSeeds(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _channel = new MasterChannel(client, selfIndex, "soul seed", log);
    }

    public async Task<(bool Loaded, SoulSeedState? State)> LoadAsync(ulong charUid, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.TpSoulSeedLoad, InterServerOpcode.TpSoulSeedLoadAckOk,
            InterServerOpcode.TpSoulSeedLoadAckCancel, [], ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? (true, SoulSeedMessages.DecodeState(answer.Body)) : (false, null);
    }

    public async Task<SoulSeedUseResult?> UseAsync(ulong charUid, SoulSeedUseRequest request, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.McmdSoulSeedUse, InterServerOpcode.McmdSoulSeedUseAckOk,
            InterServerOpcode.McmdSoulSeedUseAckCancel, SoulSeedMessages.EncodeUse(request), ct).ConfigureAwait(false);
        if (reply is not { } answer) return null;
        return answer.Ok ? new SoulSeedUseResult(true, 0) : new SoulSeedUseResult(false, SoulSeedMessages.DecodeCode(answer.Body));
    }

    public Task SavePointsAsync(ulong charUid, IReadOnlyList<short> points, CancellationToken ct) =>
        _channel.SendAsync(charUid, InterServerOpcode.McmdSoulSeedUpdate, SoulSeedMessages.EncodePoints(points), ct);

    public async Task<bool> ResetAwakeningAsync(ulong charUid, ushort skillId, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.CcmdGuardianAwakenReset, InterServerOpcode.CcmdGuardianAwakenResetAckOk,
            InterServerOpcode.CcmdGuardianAwakenAckCancel, SoulSeedMessages.EncodeReset(skillId), ct).ConfigureAwait(false);
        return reply is { Ok: true };
    }

    public async Task<GuardianAwakened?> AwakenAsync(GuardianAwakening awakening, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(awakening);
        var reply = await _channel.CallAsync(awakening.CharacterId, InterServerOpcode.CcmdGuardianAwaken, InterServerOpcode.CcmdGuardianAwakenAckOk,
            InterServerOpcode.CcmdGuardianAwakenAckCancel, SoulSeedMessages.EncodeAwaken(awakening), ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? SoulSeedMessages.DecodeAwakened(answer.Body) : null;
    }
}
