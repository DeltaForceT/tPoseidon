using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterShadow
{
    private readonly Func<MasterServerClient?> _client;
    private readonly MasterChannel _channel;

    public MasterShadow(Func<MasterServerClient?> client, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _channel = new MasterChannel(client, selfIndex, "shadow", log);
    }

    public bool Connected => _client() is { IsConnected: true };

    public Task InsertResultAsync(ShadowBonusRecord record, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(record);
        var body = ShadowMessages.EncodeResultInsert(new ShadowResultInsert(record.CharacterId, record.RewardId, record.ExpiresAt));
        return _channel.SendAsync(record.CharacterId, InterServerOpcode.McmdShadowResultInsert, body, ct);
    }

    public Task AnnounceAreaRewardAsync(ShadowAreaReward reward, CancellationToken ct) =>
        _channel.SendAsync(0, InterServerOpcode.McmdShadowAreaReward, ShadowMessages.EncodeAreaReward(reward), ct);
}
