using System.Text;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.Trident;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class CardBattleServer
{
    private readonly CardBattleService _battles;
    private readonly SocialRoom _room;
    private readonly GameServerLinks _games;
    private readonly Encoding _text;
    private readonly Action<string> _log;

    public CardBattleServer(CardBattleService battles, SocialRoom room, GameServerLinks games, Encoding text, Action<string> log)
    {
        _battles = battles ?? throw new ArgumentNullException(nameof(battles));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, TridentSessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        On(dispatcher, ctx, CardBattleFrames.MakeBattle, (member, _) => _battles.Make(Player(member)));
        On(dispatcher, ctx, CardBattleFrames.Ask, (member, p) =>
            CardBattleFrames.TryReadText(p, _text, out var target) ? _battles.Ask(Player(member), target) : null);
        On(dispatcher, ctx, CardBattleFrames.AnswerYes, (member, _) => _battles.Answer(Player(member), yes: true));
        On(dispatcher, ctx, CardBattleFrames.AnswerNo, (member, _) => _battles.Answer(Player(member), yes: false));
        On(dispatcher, ctx, CardBattleFrames.Leave, (member, p) => p.Length >= 1 ? _battles.Leave(member.Character.CharacterId, p[0]) : null);
        On(dispatcher, ctx, CardBattleFrames.SelectRule, (member, p) => p.Length >= 1 ? _battles.SelectRule(member.Character.CharacterId, p[0]) : null);
        On(dispatcher, ctx, CardBattleFrames.Deck, (member, p) =>
            CardBattleFrames.TryReadDeck(p, out var deck) ? _battles.SubmitDeck(member.Character.CharacterId, deck) : null);
        On(dispatcher, ctx, CardBattleFrames.Drop, (member, p) =>
            CardBattleFrames.TryReadDrop(p, out var slot, out var card) ? _battles.DropCard(member.Character.CharacterId, slot, card) : null);
        On(dispatcher, ctx, CardBattleFrames.Chance, (member, _) => _battles.Chance(member.Character.CharacterId));
        On(dispatcher, ctx, CardBattleFrames.ReserveExit, (member, _) => _battles.ReserveExit(member.Character.CharacterId));
        On(dispatcher, ctx, CardBattleFrames.Chat, (member, p) =>
            CardBattleFrames.TryReadText(p, _text, out var line) ? _battles.ChatLine(member.Character.CharacterId, line) : null);
        On(dispatcher, ctx, CardBattleFrames.ExtraCardRequest, (member, _) => _battles.ExtraCard(member.Character.CharacterId));
        On(dispatcher, ctx, CardBattleFrames.ResultOk, (member, _) => _battles.ResultOk(member.Character.CharacterId));
    }

    public Task ReportAsync(OfficerOrder report, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (report.OrderType == CardBattleFrames.OrderCheckReply)
        {
            if (CardBattleFrames.ReadCheckReply(report.Data) is not { } check)
            {
                _log($"[cardbattle] ORDER 0x{CardBattleFrames.OrderCheckReply:X2} from 0x{report.CharacterId:X}: {report.Data.Length} B is shorter than the check reply");
                return Task.CompletedTask;
            }
            var checkOutbox = _battles.CheckReported(report.CharacterId, check);
            foreach (var note in checkOutbox.Notes) _log($"[cardbattle] ORDER 0x{CardBattleFrames.OrderCheckReply:X2} from 0x{report.CharacterId:X}: {note}");
            return DeliverAsync(checkOutbox, ct);
        }
        if (report.OrderType != CardBattleFrames.OrderStatusReply || CardBattleFrames.ReadStatusReply(report.Data) is not { } reply) return Task.CompletedTask;
        var outbox = _battles.StatusReported(reply.CharacterId, reply.Code, reply.Reward, reply.DeckReady);
        foreach (var note in outbox.Notes) _log($"[cardbattle] ORDER 0x{CardBattleFrames.OrderStatusReply:X2} from 0x{reply.CharacterId:X}: {note}");
        return DeliverAsync(outbox, ct);
    }

    public async Task RunTimersAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(CardBattleFrames.TimerTickMs));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            var outbox = _battles.Tick(CardBattleFrames.TimerTickMs);
            foreach (var note in outbox.Notes) _log($"[cardbattle] {note}");
            await DeliverAsync(outbox, ct).ConfigureAwait(false);
        }
    }

    public Task ClosedAsync(SocialMember member, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(member);
        return DeliverAsync(_battles.Closed(member.Character.CharacterId), ct);
    }

    private static CardBattlePlayer Player(SocialMember member) =>
        new(member.Character.CharacterId, member.Character.Name, member.Character.Level);

    private delegate CardBattleOutbox? BattleRequest(SocialMember member, ReadOnlySpan<byte> payload);

    private void On(OpcodeDispatcher dispatcher, TridentSessionContext ctx, ushort opcode, BattleRequest request) =>
        dispatcher.Register(opcode, (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { } member) return Task.CompletedTask;
            if (request(member, payload.Span) is not { } outbox)
            {
                _log($"[cardbattle] 0x{opcode:X4} from 0x{member.Character.CharacterId:X} dropped: {payload.Length} B is shorter than the request");
                return Task.CompletedTask;
            }
            foreach (var note in outbox.Notes) _log($"[cardbattle] 0x{opcode:X4} from 0x{member.Character.CharacterId:X}: {note}");
            if (outbox.Steal is { } steal)
            {
                _log($"[cardbattle] card {steal.ItemId} of 0x{steal.LoserId:X} won by 0x{steal.WinnerId:X}: {(steal.TakenFromLoser ? "the game server moves it out of the loser's bag" : "the winner gets a copy, the loser keeps the card")}");
            }
            return DeliverAsync(outbox, ct);
        });

    private async Task DeliverAsync(CardBattleOutbox outbox, CancellationToken ct)
    {
        foreach (var (characterId, orderType, data) in outbox.Orders)
        {
            if (await _games.TrySendOnceAsync(characterId, orderType, data, ct).ConfigureAwait(false) || orderType != CardBattleFrames.OrderCheck) continue;
            _log($"[cardbattle] ORDER 0x{orderType:X2} to 0x{characterId:X} not sent");
            await DeliverAsync(_battles.CheckNotSent(characterId), ct).ConfigureAwait(false);
        }
        foreach (var message in outbox.Messages)
        {
            if (_room.Find(message.To) is not { } recipient) continue;
            try
            {
                await recipient.Context.SendFrameAsync(message.Opcode, message.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[cardbattle] 0x{message.Opcode:X4} to 0x{message.To:X} not delivered: {ex.Message}");
            }
        }
    }
}
