using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterHonorTitles
{
    private readonly MasterChannel _channel;

    public MasterHonorTitles(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _channel = new MasterChannel(client, selfIndex, "honor title", log);
    }

    public async Task<HonorTitleLogin?> LoadAsync(ulong charUid, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.TpHonorTitleLoad, InterServerOpcode.TpHonorTitleLoadAckOk,
            InterServerOpcode.TpHonorTitleLoadAckCancel, [], ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? HonorTitleMessages.DecodeLogin(answer.Body) : null;
    }

    public async Task<HonorTitleAdded?> AddAsync(ulong charUid, HonorTitleAdd add, CancellationToken ct)
    {
        var reply = await _channel.CallAsync(charUid, InterServerOpcode.McmdHonorTitleAdd, InterServerOpcode.McmdHonorTitleAdded,
            null, HonorTitleMessages.EncodeAdd(add), ct).ConfigureAwait(false);
        return reply is { Ok: true } answer ? HonorTitleMessages.DecodeAdded(answer.Body) : null;
    }

    public Task SaveProfileTitleAsync(ulong charUid, ushort titleType, ulong titleId, CancellationToken ct) =>
        _channel.SendAsync(charUid, InterServerOpcode.McmdProfileTitle, HonorTitleMessages.EncodeProfileTitle(titleType, titleId), ct);
}
