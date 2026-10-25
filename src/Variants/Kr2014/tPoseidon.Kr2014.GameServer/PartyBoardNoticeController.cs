using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Social;

internal sealed class PartyBoardNoticeController : IDisposable
{
    private readonly SystemServerLink _link;
    private readonly Func<ulong, InGameSessionState?> _playerById;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _sendToViewers;
    private readonly Encoding _text;
    private readonly string _logTag;

    public PartyBoardNoticeController(
        SystemServerLink link, Func<ulong, InGameSessionState?> playerById,
        Func<InGameSessionState, Opcode, byte[], Task> sendToViewers, Encoding text, string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _playerById = playerById ?? throw new ArgumentNullException(nameof(playerById));
        _sendToViewers = sendToViewers ?? throw new ArgumentNullException(nameof(sendToViewers));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
    }

    public void Dispose() => _link.OrderReceived -= OrderAsync;

    private async Task OrderAsync(OfficerOrder order, CancellationToken ct)
    {
        if (order.OrderType != PartyBoardFrames.OrderHeadNotice) return;
        if (!PartyBoardFrames.TryReadHeadNoticeOrder(order.Data, _text, out var comment, out var kind, out var active)) return;
        if (_playerById(order.CharacterId) is not { } owner) return;
        var frames = active
            ? owner.Sign.ShowPartyBoardNotice(owner.PlayerEntityId, kind, comment, _text)
            : owner.Sign.ClearPartyBoardNotice(owner.PlayerEntityId);
        foreach (var frame in frames) await _sendToViewers(owner, (Opcode)frame.Opcode, frame.Payload).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [party-board] 0x{order.CharacterId:X} {(active ? "shows" : "clears")} the recruit notice");
    }
}
