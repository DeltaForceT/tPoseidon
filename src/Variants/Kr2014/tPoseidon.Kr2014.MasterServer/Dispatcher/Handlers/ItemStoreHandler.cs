using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.Net;

namespace tPoseidon.Kr2014.MasterServer.Dispatcher.Handlers;

public sealed class ItemStoreHandler : IInterServerHandler
{
    private readonly IItemStore _items;

    public ItemStoreHandler(IItemStore items)
    {
        ArgumentNullException.ThrowIfNull(items);
        _items = items;
    }

    public async ValueTask HandleAsync(MasterServerSession session, InterServerFrame frame, MasterContext ctx, CancellationToken ct)
    {
        var charUid = frame.Header.CharUid;
        if (frame.Body.Length == 0)
        {
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            ctx.Log($"item-store char 0x{charUid:X16}: empty body; replied CANCEL");
            return;
        }

        var subOp = (ItemStoreSubOp)frame.Body[0];
        if (SubOpRequiresChar(subOp) && charUid == 0)
        {
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            ctx.Log($"item-store sub-op 0x{(byte)subOp:X2}: invalid chruid=0 from idx={frame.Header.SourceServerIndex}; replied CANCEL");
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
            ctx.Log($"item-store char 0x{charUid:X16} sub-op 0x{(byte)subOp:X2}: store threw {ex.GetType().Name}: {ex.Message}; replying CANCEL");
            await ReplyCancelAsync(session, frame, ct).ConfigureAwait(false);
            return;
        }

        await ReplyOkAsync(session, frame, responseBody, ct).ConfigureAwait(false);
    }

    private byte[] ExecuteSubOp(ItemStoreSubOp subOp, ulong charUid, byte[] body)
    {
        switch (subOp)
        {
            case ItemStoreSubOp.GrantToCharacter:
            {
                var reader = new PacketReader(body);
                var itemIndex = reader.ReadInt32();
                var itemClass = reader.ReadByte();
                var slotKind = reader.ReadByte();
                var slotPosition = reader.ReadByte();
                var stack = reader.ReadUInt16();
                var item = _items.GrantToCharacter(charUid, itemIndex, itemClass, slotKind, slotPosition, stack);
                return ItemStoreWire.EncodeItem(item);
            }

            case ItemStoreSubOp.GetAllForCharacter:
                return ItemStoreWire.EncodeItemList(_items.GetAllForCharacter(charUid));

            case ItemStoreSubOp.GetForCharacterBySlotKind:
            {
                var reader = new PacketReader(body);
                var slotKind = reader.ReadByte();
                return ItemStoreWire.EncodeItemList(_items.GetForCharacterBySlotKind(charUid, slotKind));
            }

            case ItemStoreSubOp.FindByUid:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                var found = _items.FindByUid(itemUid);
                var payload = new PacketWriter(64);
                payload.WriteByte((byte)(found is null ? 0 : 1));
                if (found is not null)
                {
                    payload.WriteBytes(ItemStoreWire.EncodeItem(found));
                }
                return payload.ToArray();
            }

            case ItemStoreSubOp.FindBySlot:
            {
                var reader = new PacketReader(body);
                var slotKind = reader.ReadByte();
                var slotPosition = reader.ReadByte();
                var found = _items.FindBySlot(charUid, slotKind, slotPosition);
                var payload = new PacketWriter(64);
                payload.WriteByte((byte)(found is null ? 0 : 1));
                if (found is not null)
                {
                    payload.WriteBytes(ItemStoreWire.EncodeItem(found));
                }
                return payload.ToArray();
            }

            case ItemStoreSubOp.MoveToSlot:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                var newSlotKind = reader.ReadByte();
                var newSlotPosition = reader.ReadByte();
                var ok = _items.MoveToSlot(itemUid, newSlotKind, newSlotPosition);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case ItemStoreSubOp.Delete:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                var ok = _items.Delete(itemUid);
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(ok ? 1 : 0));
                return payload.ToArray();
            }

            case ItemStoreSubOp.AdjustStack:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                var delta = reader.ReadInt32();
                var newStack = _items.AdjustStack(itemUid, delta);
                var payload = new PacketWriter(2);
                payload.WriteUInt16(newStack);
                return payload.ToArray();
            }

            case ItemStoreSubOp.SetStack:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                var stored = _items.SetStack(itemUid, reader.ReadUInt16());
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(stored ? 1 : 0));
                return payload.ToArray();
            }

            case ItemStoreSubOp.SetBreakedAt:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                var stored = _items.SetBreakedAt(itemUid, new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero));
                var payload = new PacketWriter(1);
                payload.WriteByte((byte)(stored ? 1 : 0));
                return payload.ToArray();
            }

            case ItemStoreSubOp.UnassignFromCharacter:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                _items.UnassignFromCharacter(itemUid);
                return Array.Empty<byte>();
            }

            case ItemStoreSubOp.AssignToCharacter:
            {
                var reader = new PacketReader(body);
                var itemUid = reader.ReadUInt64();
                var characterId = reader.ReadUInt64();
                var slotKind = reader.ReadByte();
                var slotPosition = reader.ReadByte();
                _items.AssignToCharacter(itemUid, characterId, slotKind, slotPosition);
                return Array.Empty<byte>();
            }

            default:
                throw new InvalidOperationException($"unknown sub-op 0x{(byte)subOp:X2}");
        }
    }

    private static bool SubOpRequiresChar(ItemStoreSubOp subOp) => subOp switch
    {
        ItemStoreSubOp.GrantToCharacter => true,
        ItemStoreSubOp.GetAllForCharacter => true,
        ItemStoreSubOp.GetForCharacterBySlotKind => true,
        ItemStoreSubOp.FindBySlot => true,
        _ => false,
    };

    private static Task ReplyOkAsync(MasterServerSession session, InterServerFrame request, byte[] body, CancellationToken ct) =>
        ReplyAsync(session, request, InterServerOpcode.TpItemStoreCallAckOk, body, ct);

    private static Task ReplyCancelAsync(MasterServerSession session, InterServerFrame request, CancellationToken ct) =>
        ReplyAsync(session, request, InterServerOpcode.TpItemStoreCallAckCancel, Array.Empty<byte>(), ct);

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
