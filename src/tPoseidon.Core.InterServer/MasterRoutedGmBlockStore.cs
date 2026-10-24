using tPoseidon.Core.Data;

namespace tPoseidon.Core.InterServer;

public sealed class MasterRoutedGmBlockStore : IGmBlockStore
{
    private readonly Func<MasterServerClient?> _client;
    private readonly IGmBlockStore _direct;
    private readonly MasterChannel _channel;
    private readonly Action<string> _log;

    public MasterRoutedGmBlockStore(Func<MasterServerClient?> client, IGmBlockStore direct, ushort selfIndex, Action<string> log)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _direct = direct ?? throw new ArgumentNullException(nameof(direct));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _channel = new MasterChannel(client, selfIndex, "gm block", log);
    }

    public int BlockCharacter(ulong characterId, byte blockType, ushort minutes, ulong gameMasterId)
    {
        if (!MasterConnected())
        {
            _log($"[gm block] Master not connected; block {blockType} of 0x{characterId:X} written directly");
            return _direct.BlockCharacter(characterId, blockType, minutes, gameMasterId);
        }
        var body = GmBlockMessages.EncodeBlock(new GmBlockRequest(characterId, blockType, minutes, gameMasterId));
        return Result(_channel.CallAsync(characterId, InterServerOpcode.CcmdBlock, InterServerOpcode.CcmdBlockAckOk,
            InterServerOpcode.CcmdBlockAckCancel, body, CancellationToken.None).GetAwaiter().GetResult());
    }

    public int UnblockCharacter(ulong characterId, byte blockType, ulong gameMasterId)
    {
        if (!MasterConnected())
        {
            _log($"[gm block] Master not connected; unblock {blockType} of 0x{characterId:X} written directly");
            return _direct.UnblockCharacter(characterId, blockType, gameMasterId);
        }
        var body = GmBlockMessages.EncodeUnblock(new GmUnblockRequest(characterId, blockType, gameMasterId));
        return Result(_channel.CallAsync(characterId, InterServerOpcode.CcmdUnblock, InterServerOpcode.CcmdUnblockAckOk,
            InterServerOpcode.CcmdUnblockAckCancel, body, CancellationToken.None).GetAwaiter().GetResult());
    }

    public int SetUserAuthority(string characterName, byte authority) => _direct.SetUserAuthority(characterName, authority);

    public GmRunningBlock? LatestRunningBlock(ulong characterId) => _direct.LatestRunningBlock(characterId);

    private bool MasterConnected() => _client() is { IsConnected: true };

    private static int Result(MasterAnswer? answer)
    {
        if (answer is not { } reply) return GmBlockRules.NoMasterAnswer;
        return reply.Ok ? GmBlockRules.Ok : GmBlockRules.MasterCancelled;
    }
}
