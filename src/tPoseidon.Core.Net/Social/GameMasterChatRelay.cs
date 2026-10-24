using System.Text;

namespace tPoseidon.Core.Net.Social;

public enum GameMasterRelayResult
{
    NotAGameMasterLine,
    Relayed,
    LinkDown,
}

public sealed record GameMasterRelay(GameMasterRelayResult Result, string Line);

public sealed class GameMasterChatRelay
{
    private readonly SystemServerLink _link;
    private readonly Encoding _text;

    public GameMasterChatRelay(SystemServerLink link, Encoding text)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public async Task<GameMasterRelay> RelayAsync(ulong characterId, string name, string address, ReadOnlyMemory<byte> sayPayload, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(address);
        var line = ChatFrames.ReadText(sayPayload.Span, _text);
        if (!GameMasterChat.IsCommandLine(line)) return new GameMasterRelay(GameMasterRelayResult.NotAGameMasterLine, line);
        var sent = await _link.TrySendAsync(GameMasterFrames.LinkChatCommand, GameMasterFrames.ChatCommand(characterId, name, address, line, _text), ct).ConfigureAwait(false);
        return new GameMasterRelay(sent ? GameMasterRelayResult.Relayed : GameMasterRelayResult.LinkDown, line);
    }
}
