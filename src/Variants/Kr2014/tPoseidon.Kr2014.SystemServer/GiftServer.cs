using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class GiftServer
{
    private readonly IGiftStore _gifts;
    private readonly SocialRoom _room;
    private readonly Encoding _text;
    private readonly Action<string> _log;

    public GiftServer(IGiftStore gifts, SocialRoom room, Encoding text, Action<string> log)
    {
        _gifts = gifts ?? throw new ArgumentNullException(nameof(gifts));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public Func<ulong, string, string?>? GiftAccepted { get; set; }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        dispatcher.Register((Core.Common.Opcode)GiftFrames.SessionOpen, (_, _, _) => Open(ctx));
        dispatcher.Register((Core.Common.Opcode)GiftFrames.Accept, (_, payload, ct) => AcceptAsync(ctx, payload, ct));
        dispatcher.Register((Core.Common.Opcode)GiftFrames.ReceivedRequest, (_, _, ct) =>
            ListAsync(ctx, "received list", GiftFrames.ReceivedList, account =>
                _gifts.Received(account, GiftItemRules.ListRowLimit + 1) is { Count: > 0 } rows ? GiftFrames.Received(rows, _text) : null, ct));
        dispatcher.Register((Core.Common.Opcode)GiftFrames.RemainCountRequest, (_, _, ct) =>
            ListAsync(ctx, "remain count", GiftFrames.RemainCount, account => GiftFrames.Remain(_gifts.RemainCount(account)), ct));
        dispatcher.Register((Core.Common.Opcode)GiftFrames.PendingRequest, (_, _, ct) =>
            ListAsync(ctx, "pending list", GiftFrames.PendingList, account => GiftFrames.Pending(_gifts.Pending(account, GiftItemRules.ListRowLimit + 1), _text), ct));
        dispatcher.Register((Core.Common.Opcode)GiftFrames.SentRequest, (_, _, ct) =>
            ListAsync(ctx, "sent list", GiftFrames.SentList, account =>
                _gifts.Sent(account, GiftItemRules.ListRowLimit + 1) is { Count: > 0 } rows ? GiftFrames.Sent(rows, _text) : null, ct));
    }

    private async Task ListAsync(S2SessionContext ctx, string what, ushort reply, Func<uint, byte[]?> build, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { GiftOpen: true } member || member.Character.OwnerAccountId == 0)
        {
            _log($"[gift] {ctx.Remote} {what}: Request Failed (no gift session)");
            return;
        }
        if (build(member.Character.OwnerAccountId) is not { } payload)
        {
            _log($"[gift] 0x{member.Character.CharacterId:X} {what}: no rows, no reply");
            return;
        }
        try
        {
            await ctx.SendFrameAsync(reply, payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[gift] 0x{reply:X4} to 0x{member.Character.CharacterId:X} not delivered: {ex.Message}");
        }
    }

    private Task Open(S2SessionContext ctx)
    {
        if (_room.Find(ctx) is { } member) member.GiftOpen = true;
        return Task.CompletedTask;
    }

    private async Task AcceptAsync(S2SessionContext ctx, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_room.Find(ctx) is not { GiftOpen: true } member)
        {
            _log($"[gift] {ctx.Remote} accept: Request Failed (no gift session)");
            return;
        }
        if (!GiftFrames.TryReadAccept(payload.Span, _text, out var request))
        {
            _log($"[gift] 0x{member.Character.CharacterId:X} accept of {payload.Length} B not read");
            return;
        }
        var result = _gifts.Accept(request.GiftCode, request.ReceiveMessage, DateTimeOffset.UtcNow);
        _log($"[gift] 0x{member.Character.CharacterId:X} accepts gift {request.GiftCode} from '{request.SenderName}': {(result.Succeeded ? "granted" : $"error {result.Error}")}");
        try
        {
            await ctx.SendFrameAsync(GiftFrames.Accepted, GiftFrames.AcceptResult(result.Success), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
        {
            _log($"[gift] 0x{GiftFrames.Accepted:X4} to 0x{member.Character.CharacterId:X} not delivered: {ex.Message}");
        }
        if (result.Succeeded && GiftAccepted?.Invoke(member.Character.CharacterId, request.SenderName) is { } note) _log($"[couple] {note}");
    }
}
