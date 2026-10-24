using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class PartyServer
{
    private readonly PartyService _parties;
    private readonly SocialRoom _room;
    private readonly GameServerLinks _games;
    private readonly Encoding _text;
    private readonly Action<string> _log;

    public PartyServer(PartyService parties, SocialRoom room, GameServerLinks games, Encoding text, Action<string> log)
    {
        _parties = parties ?? throw new ArgumentNullException(nameof(parties));
        _room = room ?? throw new ArgumentNullException(nameof(room));
        _games = games ?? throw new ArgumentNullException(nameof(games));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public void Register(OpcodeDispatcher dispatcher, S2SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(ctx);
        On(dispatcher, ctx, PartyFrames.Make, (id, p) =>
        {
            var r = new PacketReader(p);
            var partyName = r.ReadNulTerminated(_text);
            return _parties.Make(id, partyName, r.ReadNulTerminated(_text));
        });
        On(dispatcher, ctx, PartyFrames.ChangeDivision, (id, p) =>
        {
            var r = new PacketReader(p);
            var type = r.ReadByte();
            var shares = new List<PartyDivisionShare>();
            if (type == PartyDivision.Custom)
            {
                var count = r.ReadByte();
                for (var i = 0; i < count; i++) shares.Add(new PartyDivisionShare(r.ReadUInt16(), r.ReadByte()));
            }
            return _parties.ChangeDivision(id, type, shares);
        });
        On(dispatcher, ctx, PartyFrames.Rename, (id, p) => _parties.Rename(id, new PacketReader(p).ReadNulTerminated(_text)));
        On(dispatcher, ctx, PartyFrames.ChangeMaster, (id, p) => _parties.ChangeMaster(id, new PacketReader(p).ReadUInt16()));
        On(dispatcher, ctx, PartyFrames.Invite, (id, p) => _parties.Invite(id, new PacketReader(p).ReadNulTerminated(_text)));
        On(dispatcher, ctx, PartyFrames.AcceptInvite, (id, _) => Joined(id, _parties.AcceptInvite(id)));
        On(dispatcher, ctx, PartyFrames.RefuseInvite, (id, p) => _parties.RefuseInvite(id, new PacketReader(p).ReadByte()));
        On(dispatcher, ctx, PartyFrames.Leave, (id, _) =>
        {
            var remaining = _parties.PartyMembers(id).Where(member => member != id).ToArray();
            var outbox = _parties.Leave(id);
            if (remaining.Length > 0) Changed(_parties.PartyMembers(remaining[0]));
            return outbox;
        });
        On(dispatcher, ctx, PartyFrames.Kick, (id, p) => _parties.Kick(id, new PacketReader(p).ReadUInt16()));
        On(dispatcher, ctx, PartyFrames.Chat, (id, p) => _parties.Chat(id, new PacketReader(p).ReadNulTerminated(_text)));
        On(dispatcher, ctx, PartyFrames.KickOffline, (id, p) => _parties.KickOffline(id, new PacketReader(p).ReadUInt64()));
        On(dispatcher, ctx, PartyFrames.SetItemShare, (id, p) => _parties.SetItemShare(id, new PacketReader(p).ReadByte()));
        On(dispatcher, ctx, PartyFrames.MemberFlagOn, (id, p) => _parties.SetMemberFlag(id, new PacketReader(p).ReadUInt64(), raised: true));
        On(dispatcher, ctx, PartyFrames.MemberFlagOff, (id, p) => _parties.SetMemberFlag(id, new PacketReader(p).ReadUInt64(), raised: false));
        On(dispatcher, ctx, PartyFrames.AskToJoin, (id, p) =>
        {
            var r = new PacketReader(p);
            var partyId = r.ReadUInt16();
            var askerName = r.ReadNulTerminated(_text);
            var x = r.ReadUInt16();
            return _parties.AskToJoin(id, partyId, askerName, x, r.ReadUInt32());
        });
        On(dispatcher, ctx, PartyFrames.AskToJoinThroughMember, (id, p) =>
        {
            var r = new PacketReader(p);
            var memberName = r.ReadNulTerminated(_text);
            var askerName = r.ReadNulTerminated(_text);
            var x = r.ReadUInt16();
            return _parties.AskToJoinThroughMember(id, memberName, askerName, x, r.ReadUInt32());
        });
        On(dispatcher, ctx, PartyFrames.AcceptJoin, (id, _) => Joined(id, _parties.AcceptJoin(id)));
        On(dispatcher, ctx, PartyFrames.RefuseJoin, (id, _) => _parties.RefuseJoin(id));
        On(dispatcher, ctx, PartyFrames.Dissolve, (id, _) => _parties.Dissolve(id));
        On(dispatcher, ctx, PartyFrames.SetSecondOption, (id, p) => _parties.SetSecondOption(id, new PacketReader(p).ReadByte()));
        On(dispatcher, ctx, PartyFrames.InfoRequest, (id, p) => _parties.Info(id, new PacketReader(p).ReadNulTerminated(_text)));
        On(dispatcher, ctx, PartyFrames.CancelInvite, (id, _) => _parties.CancelInvite(id));
        On(dispatcher, ctx, PartyFrames.RemovePartyQuest, (id, _) => _parties.TearDownQuest(id));
    }

    public Task LoggedInAsync(SocialMember member, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(member);
        var c = member.Character;
        return DeliverAsync(_parties.Login(c.CharacterId, c.Name, FriendRules.CharacterTypeOf(c), c.Level), ct);
    }

    public Task ClosedAsync(SocialMember member, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(member);
        return DeliverAsync(_parties.Logout(member.Character.CharacterId), ct);
    }

    public Task ZoneEnteredAsync(ulong characterId, ushort zone, CancellationToken ct) =>
        DeliverAsync(_parties.ZoneEntered(characterId, zone), ct);

    public bool BeginQuest(PartyQuestBegin begin) => _parties.BeginQuest(begin);

    public Task KindChangedAsync(ulong characterId, byte kind, byte job, CancellationToken ct) =>
        DeliverAsync(_parties.KindChanged(characterId, kind, job), ct);

    public Task DeadChangedAsync(ulong characterId, bool dead, CancellationToken ct) =>
        DeliverAsync(_parties.SetDead(characterId, dead), ct);

    public Task QuestKillAsync(ulong characterId, ushort monsterId, CancellationToken ct) =>
        DeliverAsync(_parties.QuestKill(characterId, monsterId), ct);

    public void CancelQuest(ulong characterId, uint questId) => _parties.CancelQuest(characterId, questId);

    public Task ZoneLeftAsync(ulong characterId, CancellationToken ct) =>
        DeliverAsync(_parties.ZoneLeft(characterId), ct);

    public async Task ExpireOfflineMembersAsync(TimeSpan period, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(period);
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                await DeliverAsync(_parties.ExpireOfflineMembers(), ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    public Action<IReadOnlyList<ulong>>? PartyChanged { get; set; }

    private PartyOutbox Joined(ulong characterId, PartyOutbox outbox)
    {
        Changed(_parties.PartyMembers(characterId));
        return outbox;
    }

    private void Changed(IReadOnlyList<ulong> members)
    {
        if (members.Count > 0) PartyChanged?.Invoke(members);
    }

    private delegate PartyOutbox PartyRequest(ulong characterId, ReadOnlySpan<byte> payload);

    private void On(OpcodeDispatcher dispatcher, S2SessionContext ctx, ushort opcode, PartyRequest request) =>
        dispatcher.Register(opcode, (_, payload, ct) =>
        {
            if (_room.Find(ctx) is not { } member) return Task.CompletedTask;
            _log($"[party] 0x{opcode:X4} from 0x{member.Character.CharacterId:X} '{member.Character.Name}' [{Convert.ToHexString(payload.Span)}]");
            return DeliverAsync(request(member.Character.CharacterId, payload.Span), ct);
        });

    private async Task DeliverAsync(PartyOutbox messages, CancellationToken ct)
    {
        foreach (var order in messages.Orders) await _games.SendAsync(order, ct).ConfigureAwait(false);
        foreach (var message in messages)
        {
            if (_room.Find(message.To) is not { } recipient)
            {
                _log($"[party] 0x{message.Opcode:X4} for 0x{message.To:X} skipped: not online");
                continue;
            }
            try
            {
                await recipient.Context.SendFrameAsync(message.Opcode, message.Payload, (byte)Random.Shared.Next(1, 256), ct).ConfigureAwait(false);
                _log($"[party] 0x{message.Opcode:X4} -> 0x{message.To:X} '{recipient.Character.Name}' ({message.Payload.Length} B)");
            }
            catch (Exception ex) when (ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
                _log($"[party] 0x{message.Opcode:X4} to 0x{message.To:X} not delivered: {ex.Message}");
            }
        }
    }
}
