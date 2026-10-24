using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Social;

internal sealed class InstantChatSignController : IDisposable
{
    private readonly SystemServerLink _link;
    private readonly Func<ulong, InGameSessionState?> _playerById;
    private readonly Func<IReadOnlyCollection<InGameSessionState>> _sessions;
    private readonly Func<InGameSessionState, Opcode, byte[], Task> _sendToViewers;
    private readonly string _logTag;

    public InstantChatSignController(
        SystemServerLink link, Func<ulong, InGameSessionState?> playerById, Func<IReadOnlyCollection<InGameSessionState>> sessions,
        Func<InGameSessionState, Opcode, byte[], Task> sendToViewers, string logTag)
    {
        _link = link ?? throw new ArgumentNullException(nameof(link));
        _playerById = playerById ?? throw new ArgumentNullException(nameof(playerById));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _sendToViewers = sendToViewers ?? throw new ArgumentNullException(nameof(sendToViewers));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _link.OrderReceived += OrderAsync;
    }

    public void Dispose() => _link.OrderReceived -= OrderAsync;

    public static async Task ShowToAsync(InGameSessionState viewer, InGameSessionState owner)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.InstantChatSign is not { } sign) return;
        await viewer.SendFrameAsync((Opcode)InstantChatFrames.SignShown, InstantChatFrames.SignShownBody(owner.PlayerEntityId, sign.RoomId),
            (byte)Random.Shared.Next(1, 256), default).ConfigureAwait(false);
    }

    private async Task OrderAsync(OfficerOrder order, CancellationToken ct)
    {
        switch (order.OrderType)
        {
            case InstantChatFrames.OrderReportLevel:
                if (_playerById(order.CharacterId) is not { } asked) return;
                var level = asked.Stats?.Level ?? asked.Character?.Level ?? 0;
                await _link.ReportAsync(order with { OrderType = InstantChatFrames.ReportLevel, Data = InstantChatFrames.LevelReportData(level) }, ct).ConfigureAwait(false);
                break;
            case InstantChatFrames.OrderMyNotice when InstantChatFrames.TryReadSignOrder(order.Data, out var roomId):
                if (_playerById(order.CharacterId) is not { } owner) return;
                owner.InstantChatSign = owner.InstantChatSign is { RoomId: var shown } held && shown == roomId
                    ? held
                    : new InstantChatSign(order.OrderId, roomId);
                await _sendToViewers(owner, (Opcode)InstantChatFrames.SignShown, InstantChatFrames.SignShownBody(owner.PlayerEntityId, roomId)).ConfigureAwait(false);
                Console.WriteLine($"{_logTag} [chat] 0x{order.CharacterId:X} shows the sign of instant chat room {roomId}");
                break;
            case InstantChatFrames.OrderNoticeState when order.Data.Length >= 1:
                if (_playerById(order.CharacterId) is not { } member) return;
                member.InstantChatState = order.OrderId;
                Console.WriteLine($"{_logTag} [chat] 0x{order.CharacterId:X} is in an instant chat room ({(order.Data[0] != 0 ? "master" : "member")}); move, engage and pickup are held");
                break;
            case SystemLinkFrames.OrderCancel:
                var cancelled = SystemLinkFrames.ReadCancelledOrderId(order.Data);
                foreach (var session in _sessions())
                {
                    if (session.InstantChatState == cancelled)
                    {
                        session.InstantChatState = null;
                    }
                    if (session.InstantChatSign?.OrderId != cancelled) continue;
                    session.InstantChatSign = null;
                    await _sendToViewers(session, (Opcode)InstantChatFrames.SignRemoved, InstantChatFrames.SignRemovedBody(session.PlayerEntityId)).ConfigureAwait(false);
                    Console.WriteLine($"{_logTag} [chat] 0x{session.Character?.CharacterId:X} removed the sign of an instant chat room");
                }
                break;
        }
    }
}

internal sealed record InstantChatSign(ushort OrderId, ushort RoomId);
