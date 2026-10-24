using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class CoupleServer
{
    private readonly CoupleService _couples;
    private readonly IWeddingBoardStore _board;
    private readonly WeddingHallService _halls;
    private readonly System.Text.Encoding _text;
    private readonly SocialRoom _room;
    private readonly GameServerLinks _games;
    private readonly Func<ushort, int?> _zoneType;
    private readonly Action<string> _log;

    public CoupleServer(
        CoupleService couples, WeddingHallService halls, IWeddingBoardStore board, SocialRoom room, GameServerLinks games, Func<ushort, int?> zoneType, System.Text.Encoding text, Action<string> log)
    {
        _zoneType = zoneType ?? throw new ArgumentNullException(nameof(zoneType));
        _couples = couples ?? throw new ArgumentNullException(nameof(couples));
        _halls = halls ?? throw new ArgumentNullException(nameof(halls));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _board = board ?? throw new ArgumentNullException(nameof(board));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        On(dispatcher, ctx, CoupleSocialFrames.SessionOpen, (id, _) => _couples.OpenSession(id));
        On(dispatcher, ctx, CoupleSocialFrames.LoverPosition, (id, _) => _couples.LoverPosition(id, _games.ZoneOf));
        On(dispatcher, ctx, CoupleSocialFrames.HallListRequest, (id, _) => _couples.HallList(id, _halls.Listings()));
        Hall(dispatcher, ctx, CoupleSocialFrames.MoveToLover, (member, p) =>
            CoupleSocialFrames.TryReadMoveToLover(p, out var loverZone) ? _couples.MoveToLover(member.Character.CharacterId, loverZone, _zoneType) : null);
        Hall(dispatcher, ctx, WeddingHallFrames.MakeHall, (member, p) =>
            WeddingHallFrames.TryReadMakeHall(p, _text, out var title, out var password, out var mapId)
                ? _halls.Make(member.Character.CharacterId, member.Character.Name, title, password, mapId) : null);
        Hall(dispatcher, ctx, WeddingHallFrames.JoinHall, (member, p) =>
            WeddingHallFrames.TryReadJoin(p, _text, out var wdid, out var password)
                ? _halls.Join(member.Character.CharacterId, member.Character.Name, wdid, password) : null);
        Hall(dispatcher, ctx, WeddingHallFrames.ExitHall, (member, p) =>
            WeddingHallFrames.TryReadHall(p, out var wdid) ? _halls.Exit(member.Character.CharacterId, wdid) : null);
        Hall(dispatcher, ctx, WeddingHallFrames.StartCeremony, (member, p) =>
            WeddingHallFrames.TryReadHall(p, out var wdid) ? _halls.Start(member.Character.CharacterId, wdid) : null);
        Hall(dispatcher, ctx, WeddingHallFrames.KickFromHall, (member, p) =>
            WeddingHallFrames.TryReadKick(p, out var wdid, out var targets) ? _halls.Kick(member.Character.CharacterId, wdid, targets) : null);
        Hall(dispatcher, ctx, WeddingHallFrames.HallDetailRequest, (member, p) =>
            WeddingHallFrames.TryReadHall(p, out var wdid) ? _halls.Detail(member.Character.CharacterId, wdid) : null);
        On(dispatcher, ctx, CoupleSocialFrames.ArticleListRequest, (id, _) => _couples.ArticleList(id, _board.Articles) ?? Refused(id, "article list", "no couple session"));
        On(dispatcher, ctx, CoupleSocialFrames.WriteArticle, (id, p) => Logged(id, "article write", _couples.WriteArticle(id, p, _board, modify: false)));
        On(dispatcher, ctx, CoupleSocialFrames.ModifyArticle, (id, p) => Logged(id, "article modify", _couples.WriteArticle(id, p, _board, modify: true)));
        On(dispatcher, ctx, CoupleSocialFrames.DeleteArticle, (id, _) => Logged(id, "article delete", _couples.DeleteArticle(id, _board)));
        On(dispatcher, ctx, CoupleSocialFrames.BreakUp, (id, _) => Logged(id, "break up", _couples.BreakUp(id)));
        On(dispatcher, ctx, CoupleSocialFrames.RequestDivorce, (id, _) => Logged(id, "divorce request", _couples.RequestDivorce(id)));
        On(dispatcher, ctx, CoupleSocialFrames.DivorceAnswer, (id, p) =>
            CoupleSocialFrames.TryReadAnswer(p, out var accept) ? Logged(id, "divorce answer", _couples.AnswerDivorce(id, accept)) : null);
    }

    public Task LinkMessageAsync(ushort opcode, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        switch (opcode)
        {
            case CoupleSocialFrames.LinkMakeCouple when CoupleSocialFrames.TryReadMakeCouple(payload.Span, out var male, out var female):
                return DeliverAsync(Logged(male, $"make couple with 0x{female:X}", _couples.MakeCouple(male, female)), ct);
            case CoupleSocialFrames.LinkCoupleInfoRequest when CoupleSocialFrames.TryReadCharacter(payload.Span, out var characterId):
                return DeliverAsync(_couples.Refresh(characterId), ct);
            case WeddingHallFrames.LinkEndWedding when WeddingHallFrames.ReadEndWedding(payload.Span) is { } ended:
                return DeliverAsync("end wedding", _halls.Ended(ended), ct);
            case CoupleSocialFrames.LinkMarriageOption when CoupleSocialFrames.TryReadMarriageOption(payload.Span, out var code, out var ring, out var dress, out var title):
                _log($"[couple] couple {code} options ring {ring} dress {dress} title {title}");
                return Task.CompletedTask;
            default:
                _log($"[couple] link 0x{opcode:X4} of {payload.Length} B not read");
                return Task.CompletedTask;
        }
    }

    public Task ZoneEnteredAsync(ulong characterId, ushort zone, CancellationToken ct) =>
        DeliverAsync($"0x{characterId:X} entered zone {zone}", _halls.EnteredZone(characterId, zone), ct);

    public Task ReportAsync(OfficerOrder report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.OrderType == CoupleSocialFrames.OrderLoverPosition) return LoverSpotAsync(report, ct);
        if (report.OrderType != WeddingHallFrames.ReportEnterMember) return Task.CompletedTask;
        if (WeddingHallFrames.ReadEnterReport(report.Data, _text) is not { } entered)
        {
            _log($"[couple] hall enter report of {report.Data.Length} B not read");
            return Task.CompletedTask;
        }
        return DeliverAsync($"hall enter report for 0x{report.CharacterId:X}", _halls.EnterReported(report.CharacterId, entered), ct);
    }

    private Task LoverSpotAsync(OfficerOrder report, CancellationToken ct)
    {
        if (!CoupleSocialFrames.TryReadLoverSpot(report.Data, out var requesterId, out var zone, out _, out _))
        {
            _log($"[couple] lover position report of {report.Data.Length} B not read");
            return Task.CompletedTask;
        }
        if (_room.Find(requesterId) is null)
        {
            _log($"[couple] lover position for 0x{requesterId:X} dropped: not online");
            return Task.CompletedTask;
        }
        var outbox = new WeddingOutbox();
        outbox.Orders.Add(new WeddingOrder(requesterId, null, CoupleSocialFrames.OrderMoveToLover, report.Data.ToArray()));
        return DeliverAsync($"move 0x{requesterId:X} to the lover in zone {zone}", outbox, ct);
    }

    public string? GiftAccepted(ulong characterId, string senderName) => _couples.GiftAccepted(characterId, senderName);

    public void Closed(SocialMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _couples.Close(member.Character.CharacterId);
    }

    private IReadOnlyList<CoupleMessage> Logged(ulong characterId, string what, (IReadOnlyList<CoupleMessage> Messages, string Outcome) result)
    {
        _log($"[couple] {what} by 0x{characterId:X}: {result.Outcome}");
        return result.Messages;
    }

    private CoupleMessage[] Refused(ulong characterId, string what, string reason)
    {
        _log($"[couple] {what} by 0x{characterId:X} refused: {reason}");
        return Array.Empty<CoupleMessage>();
    }

    private delegate IReadOnlyList<CoupleMessage>? CoupleRequest(ulong characterId, ReadOnlySpan<byte> payload);

    private void On(OpcodeDispatcher dispatcher, S2SessionContext ctx, ushort opcode, CoupleRequest request) =>
        dispatcher.Register(opcode, (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { } member) return Task.CompletedTask;
            if (request(member.Character.CharacterId, payload.Span) is not { } messages)
            {
                _log($"[couple] 0x{opcode:X4} from 0x{member.Character.CharacterId:X} dropped: {payload.Length} B is shorter than the request");
                return Task.CompletedTask;
            }
            return DeliverAsync(messages, ct);
        });

    private delegate WeddingOutbox? HallRequest(SocialMember member, ReadOnlySpan<byte> payload);

    private void Hall(OpcodeDispatcher dispatcher, S2SessionContext ctx, ushort opcode, HallRequest request) =>
        dispatcher.Register(opcode, (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { } member) return Task.CompletedTask;
            if (request(member, payload.Span) is not { } outbox)
            {
                _log($"[couple] 0x{opcode:X4} from 0x{member.Character.CharacterId:X} dropped: {payload.Length} B not read");
                return Task.CompletedTask;
            }
            return DeliverAsync($"0x{opcode:X4} from 0x{member.Character.CharacterId:X}", outbox, ct);
        });

    private async Task DeliverAsync(string what, WeddingOutbox outbox, CancellationToken ct)
    {
        foreach (var note in outbox.Notes) _log($"[couple] {what}: {note}");
        await DeliverAsync(outbox.Messages, ct).ConfigureAwait(false);
        foreach (var order in outbox.Orders)
        {
            if (order.CharacterId is { } characterId)
            {
                await _games.SendOnceAsync(characterId, order.OrderType, order.Data, ct).ConfigureAwait(false);
            }
            else if (order.Zone is { } zone)
            {
                await _games.SendToZoneAsync(zone, order.OrderType, order.Data, ct).ConfigureAwait(false);
            }
            _log($"[couple] ORDER 0x{order.OrderType:X2} -> {(order.CharacterId is { } to ? $"0x{to:X}" : $"zone {order.Zone}")} ({order.Data.Length} B)");
        }
    }

    private async Task DeliverAsync(IReadOnlyList<CoupleMessage> messages, CancellationToken ct)
    {
        foreach (var message in messages)
        {
            if (_room.Find(message.To) is not { } recipient)
            {
                _log($"[couple] 0x{message.Opcode:X4} for 0x{message.To:X} skipped: not online");
                continue;
            }
            try
            {
                await recipient.Context.SendFrameAsync(message.Opcode, message.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
                _log($"[couple] 0x{message.Opcode:X4} -> 0x{message.To:X} ({message.Payload.Length} B)");
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[couple] 0x{message.Opcode:X4} to 0x{message.To:X} not delivered: {ex.Message}");
            }
        }
    }
}
