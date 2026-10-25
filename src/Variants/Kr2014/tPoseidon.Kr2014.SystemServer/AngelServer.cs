using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class AngelServer
{
    private readonly AngelService _angels;
    private readonly SocialRoom _room;
    private readonly GameServerLinks _games;
    private readonly Encoding _text;
    private readonly IFriendStore _friends;
    private readonly Action<string> _log;

    public AngelServer(AngelService angels, SocialRoom room, GameServerLinks games, IFriendStore friends, Encoding text, Action<string> log)
    {
        _friends = friends ?? throw new ArgumentNullException(nameof(friends));
        _angels = angels ?? throw new ArgumentNullException(nameof(angels));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        On(dispatcher, ctx, AngelFrames.SessionOpen, (member, _) => _angels.OpenSession(member.Character.CharacterId));
        On(dispatcher, ctx, AngelFrames.ListRequest, (member, _) => _angels.List(member.Character.CharacterId));
        On(dispatcher, ctx, AngelFrames.Call, (member, p) =>
            AngelFrames.ReadCall(p, _text) is { } call
                ? _angels.Call(member.Character.CharacterId, member.Character.Name, member.Character.Level,
                    _games.ZoneOf(member.Character.CharacterId) ?? 0, call)
                : null);
        On(dispatcher, ctx, AngelFrames.Dismiss, (member, p) =>
            AngelFrames.TryReadFlag(p, out var rude) ? _angels.Dismiss(member.Character.CharacterId, rude) : null);
        On(dispatcher, ctx, AngelFrames.Accept, (member, p) =>
            AngelFrames.TryReadAccept(p, out var zone, out var x, out var y) ? _angels.Accept(member.Character.CharacterId, zone, x, y) : null);
        On(dispatcher, ctx, AngelFrames.Portal, (member, p) =>
            AngelFrames.TryReadPortal(p, out var goBack, out var rude) ? _angels.Portal(member.Character.CharacterId, goBack, rude) : null);
        On(dispatcher, ctx, AngelFrames.RemoveListed, (member, p) =>
            AngelFrames.TryReadCharacter(p, out var angelId) ? _angels.RemoveListed(member.Character.CharacterId, angelId) : null);
        On(dispatcher, ctx, AngelFrames.Busy, (member, _) => _angels.Busy(member.Character.CharacterId));
    }

    public Task LoggedInAsync(SocialMember member, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(member);
        return DeliverAsync(_angels.LoggedIn(member.Character.CharacterId, member.Character.Level), ct);
    }

    public Task LevelChangedAsync(ulong characterId, uint level, CancellationToken ct)
    {
        if (_room.Find(characterId) is not { } member) return Task.CompletedTask;
        var outbox = _angels.LevelChanged(characterId, member.Character.OwnerAccountId, (int)level);
        foreach (var note in outbox.Notes) _log($"[angel] {note}");
        return DeliverGraduationAsync(outbox, member.Character.Name, ct);
    }

    public void Closed(SocialMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        _angels.LoggedOut(member.Character.CharacterId);
    }

    public Task ReportAsync(tPoseidon.Core.Net.Social.OfficerOrder report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.OrderType != AngelFrames.OrderNewbiePosition || AngelFrames.ReadSpot(report.Data) is not { } spot) return Task.CompletedTask;
        return DeliverAsync(_angels.NewbieSpot(spot, id => _room.Find(id)?.Character.Name), ct);
    }

    private async Task DeliverGraduationAsync(AngelOutbox outbox, string newbieName, CancellationToken ct)
    {
        await DeliverAsync(outbox, ct).ConfigureAwait(false);
        foreach (var angel in outbox.GraduationMemos)
        {
            if (_room.Find(angel) is { } online)
            {
                try
                {
                    await online.Context.SendFrameAsync(AngelFrames.GraduationMemo, AngelFrames.GraduationMemoFor(newbieName, _text), (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
                {
                    _log($"[angel] graduation memo to 0x{angel:X} not delivered: {ex.Message}");
                }
                continue;
            }
            var stored = _friends.SendMessage(angel, FriendRules.GmSenderCharacterId, FriendRules.GmMessageKind, AngelFrames.GraduationMemoKey);
            _log($"[angel] graduation memo for the offline angel 0x{angel:X}: {(stored ? "stored" : "not stored")}");
        }
    }

    private delegate AngelOutbox? AngelRequest(SocialMember member, ReadOnlySpan<byte> payload);

    private void On(OpcodeDispatcher dispatcher, S2SessionContext ctx, ushort opcode, AngelRequest request) =>
        dispatcher.Register(opcode, (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { } member) return Task.CompletedTask;
            if (request(member, payload.Span) is not { } outbox)
            {
                _log($"[angel] 0x{opcode:X4} from 0x{member.Character.CharacterId:X} dropped: {payload.Length} B is shorter than the request");
                return Task.CompletedTask;
            }
            foreach (var note in outbox.Notes) _log($"[angel] 0x{opcode:X4} from 0x{member.Character.CharacterId:X}: {note}");
            return DeliverAsync(outbox, ct);
        });

    private async Task DeliverAsync(AngelOutbox outbox, CancellationToken ct)
    {
        foreach (var message in outbox.Messages)
        {
            if (_room.Find(message.To) is not { } recipient)
            {
                _log($"[angel] 0x{message.Opcode:X4} for 0x{message.To:X} skipped: not online");
                continue;
            }
            try
            {
                await recipient.Context.SendFrameAsync(message.Opcode, message.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[angel] 0x{message.Opcode:X4} to 0x{message.To:X} not delivered: {ex.Message}");
            }
        }
        foreach (var order in outbox.Orders)
        {
            await _games.SendOnceAsync(order.CharacterId, order.OrderType, order.Data, ct).ConfigureAwait(false);
        }
        foreach (var direct in outbox.Direct)
        {
            await _games.SendDirectAsync(direct.To, direct.Opcode, direct.Payload, ct).ConfigureAwait(false);
        }
    }
}
