using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class CharStoreHandler : IInterServerHandler
{
    private readonly ICharStore _chars;

    public CharStoreHandler(ICharStore chars)
    {
        ArgumentNullException.ThrowIfNull(chars);
        _chars = chars;
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        if (frame.Body.Length == 0)
        {
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            ctx.Log($"char-store: empty body from idx={frame.Header.SourceServerIndex}; replied CANCEL");
            return;
        }

        var subOp = (CharStoreSubOp)frame.Body[0];
        var charUid = frame.Header.CharUid;
        if (SubOpRequiresChar(subOp) && charUid == 0)
        {
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            ctx.Log($"char-store sub-op 0x{(byte)subOp:X2}: invalid chruid=0 from idx={frame.Header.SourceServerIndex}; replied CANCEL");
            return;
        }
        var body = new byte[frame.Body.Length - 1];
        Buffer.BlockCopy(frame.Body, 1, body, 0, body.Length);

        byte[] responseBody;
        try
        {
            responseBody = ExecuteSubOp(subOp, charUid, body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ctx.Log($"char-store char 0x{charUid:X16} sub-op 0x{(byte)subOp:X2}: store threw {ex.GetType().Name}: {ex.Message}; replying CANCEL");
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            return;
        }

        await ReplyOkAsync(session, frame, responseBody, ct).ConfigureAwait(false);
    }

    private byte[] ExecuteSubOp(CharStoreSubOp subOp, ulong charUid, byte[] body)
    {
        switch (subOp)
        {
            case CharStoreSubOp.GetCharactersForAccount:
            {
                var reader = new PacketReader(body);
                var accountId = reader.ReadUInt32();
                return CharStoreWire.EncodeCharacterList(_chars.GetCharactersForAccount(accountId));
            }

            case CharStoreSubOp.GetCharacterById:
            {
                var found = _chars.GetCharacterById(charUid);
                var payload = new PacketWriter(256);
                payload.WriteByte((byte)(found is null ? 0 : 1));
                if (found is not null)
                {
                    payload.WriteBytes(CharStoreWire.EncodeCharacter(found));
                }
                return payload.ToArray();
            }

            case CharStoreSubOp.AddCharacter:
                _chars.AddCharacter(CharStoreWire.DecodeCharacter(body));
                return Array.Empty<byte>();

            case CharStoreSubOp.DeleteCharacter:
            {
                var reader = new PacketReader(body);
                var ownerAccountId = reader.ReadUInt32();
                var ok = _chars.DeleteCharacter(charUid, ownerAccountId);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case CharStoreSubOp.SetCharacterMap:
            {
                var reader = new PacketReader(body);
                var mapId = reader.ReadUInt16();
                var ok = _chars.SetCharacterMap(charUid, mapId);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case CharStoreSubOp.SetCharacterHome:
            {
                var reader = new PacketReader(body);
                var homeMapId = reader.ReadUInt16();
                var ok = _chars.SetCharacterHome(charUid, homeMapId);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case CharStoreSubOp.SetCharacterPosition:
            {
                var reader = new PacketReader(body);
                var mapId = reader.ReadUInt16();
                var positionX = reader.ReadUInt16();
                var positionY = reader.ReadUInt16();
                var ok = _chars.SetCharacterPosition(charUid, mapId, positionX, positionY);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case CharStoreSubOp.SaveCharacterStats:
            {
                var snapshot = CharStoreWire.DecodeStatsSnapshot(body);
                var ok = _chars.SaveCharacterStats(charUid, snapshot);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case CharStoreSubOp.EditCharacter:
            {
                var code = _chars.EditCharacter(charUid, CharStoreWire.DecodeEdit(body));
                var payload = new PacketWriter(4);
                payload.WriteUInt32(code);
                return payload.ToArray();
            }

            default:
                throw new InvalidOperationException($"unknown sub-op 0x{(byte)subOp:X2}");
        }
    }

    private static bool SubOpRequiresChar(CharStoreSubOp subOp) => subOp switch
    {
        CharStoreSubOp.GetCharactersForAccount => false,
        CharStoreSubOp.AddCharacter => false,
        _ => true,
    };

    private static Task ReplyOkAsync(MasterServerSession session, InterServerFrame request, byte[] body, CancellationToken ct) =>
        ReplyAsync(session, request, InterServerOpcode.TpCharStoreCallAckOk, body, ct);

    private static Task ReplyCancelAsync(MasterServerSession session, InterServerFrame request, CancellationToken ct) =>
        ReplyAsync(session, request, InterServerOpcode.TpCharStoreCallAckCancel, Array.Empty<byte>(), ct);

    private static Task ReplyAsync(MasterServerSession session, InterServerFrame request, InterServerOpcode opcode, byte[] body, CancellationToken ct)
    {
        var reply = new InterServerFrame
        {
            Opcode = opcode,
            Header = new InterServerHeader
            {
                SourceServerIndex = 0,
                DestServerIndex = request.Header.SourceServerIndex,
                CharUid = request.Header.CharUid,
                RequestId = request.Header.RequestId,
            },
            Body = body,
        };
        return session.SendAsync(reply, ct);
    }
}
