using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed class MasterRoutedItemStore : IItemStore
{
    private static readonly HashSet<InterServerOpcode> AckOpcodes = new()
    {
        InterServerOpcode.TpItemStoreCallAckOk,
        InterServerOpcode.TpItemStoreCallAckCancel,
    };

    private readonly MasterServerClient _client;
    private readonly IItemStore _fallback;
    private readonly ushort _selfIndex;
    private readonly Action<string> _log;
    private readonly TimeSpan _timeout;

    public MasterRoutedItemStore(
        MasterServerClient client,
        IItemStore fallback,
        ushort selfIndex,
        Action<string> log,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(log);
        _client = client;
        _fallback = fallback;
        _selfIndex = selfIndex;
        _log = log;
        _timeout = timeout ?? TimeSpan.FromSeconds(2);
    }

    public Item GrantToCharacter(ulong characterId, int itemIndex, byte itemClass, byte slotKind, byte slotPosition, ushort stack = 1)
    {
        try
        {
            var w = new PacketWriter(16);
            w.WriteInt32(itemIndex);
            w.WriteByte(itemClass);
            w.WriteByte(slotKind);
            w.WriteByte(slotPosition);
            w.WriteUInt16(stack);
            var reply = Invoke(characterId, ItemStoreSubOp.GrantToCharacter, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"item GrantToCharacter char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.GrantToCharacter(characterId, itemIndex, itemClass, slotKind, slotPosition, stack);
            }
            return ItemStoreWire.DecodeItem(reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item GrantToCharacter char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GrantToCharacter(characterId, itemIndex, itemClass, slotKind, slotPosition, stack);
        }
    }

    public IReadOnlyList<Item> GetAllForCharacter(ulong characterId)
    {
        try
        {
            var reply = Invoke(characterId, ItemStoreSubOp.GetAllForCharacter, Array.Empty<byte>(), out var ok);
            if (!ok)
            {
                _log($"item GetAllForCharacter char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.GetAllForCharacter(characterId);
            }
            return ItemStoreWire.DecodeItemList(reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item GetAllForCharacter char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GetAllForCharacter(characterId);
        }
    }

    public IReadOnlyList<Item> GetForCharacterBySlotKind(ulong characterId, byte slotKind)
    {
        try
        {
            var w = new PacketWriter(1);
            w.WriteByte(slotKind);
            var reply = Invoke(characterId, ItemStoreSubOp.GetForCharacterBySlotKind, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"item GetForCharacterBySlotKind char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.GetForCharacterBySlotKind(characterId, slotKind);
            }
            return ItemStoreWire.DecodeItemList(reply);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item GetForCharacterBySlotKind char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.GetForCharacterBySlotKind(characterId, slotKind);
        }
    }

    public Item? FindByUid(ulong itemUid)
    {
        try
        {
            var w = new PacketWriter(8);
            w.WriteUInt64(itemUid);
            var reply = Invoke(0, ItemStoreSubOp.FindByUid, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"item FindByUid item 0x{itemUid:X16}: master CANCEL; using local fallback");
                return _fallback.FindByUid(itemUid);
            }
            var reader = new PacketReader(reply);
            var present = reader.ReadByte() != 0;
            if (!present) return null;
            return ItemStoreWire.DecodeItem(reply.AsSpan(1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item FindByUid item 0x{itemUid:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.FindByUid(itemUid);
        }
    }

    public Item? FindBySlot(ulong characterId, byte slotKind, byte slotPosition)
    {
        try
        {
            var w = new PacketWriter(2);
            w.WriteByte(slotKind);
            w.WriteByte(slotPosition);
            var reply = Invoke(characterId, ItemStoreSubOp.FindBySlot, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"item FindBySlot char 0x{characterId:X16}: master CANCEL; using local fallback");
                return _fallback.FindBySlot(characterId, slotKind, slotPosition);
            }
            var reader = new PacketReader(reply);
            var present = reader.ReadByte() != 0;
            if (!present) return null;
            return ItemStoreWire.DecodeItem(reply.AsSpan(1));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item FindBySlot char 0x{characterId:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.FindBySlot(characterId, slotKind, slotPosition);
        }
    }

    public bool MoveToSlot(ulong itemUid, byte newSlotKind, byte newSlotPosition)
    {
        try
        {
            var w = new PacketWriter(10);
            w.WriteUInt64(itemUid);
            w.WriteByte(newSlotKind);
            w.WriteByte(newSlotPosition);
            if (!FireAndForget(0, ItemStoreSubOp.MoveToSlot, w.ToArray()))
            {
                _log($"item MoveToSlot item 0x{itemUid:X16}: master not connected; using local fallback");
                return _fallback.MoveToSlot(itemUid, newSlotKind, newSlotPosition);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item MoveToSlot item 0x{itemUid:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.MoveToSlot(itemUid, newSlotKind, newSlotPosition);
        }
    }

    public bool UnassignFromCharacter(ulong itemUid)
    {
        try
        {
            var w = new PacketWriter(8);
            w.WriteUInt64(itemUid);
            if (!FireAndForget(0, ItemStoreSubOp.UnassignFromCharacter, w.ToArray()))
            {
                _log($"item UnassignFromCharacter item 0x{itemUid:X16}: master not connected; using local fallback");
                return _fallback.UnassignFromCharacter(itemUid);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item UnassignFromCharacter item 0x{itemUid:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.UnassignFromCharacter(itemUid);
        }
    }

    public bool AssignToCharacter(ulong itemUid, ulong characterId, byte slotKind, byte slotPosition)
    {
        try
        {
            var w = new PacketWriter(18);
            w.WriteUInt64(itemUid);
            w.WriteUInt64(characterId);
            w.WriteByte(slotKind);
            w.WriteByte(slotPosition);
            if (!FireAndForget(characterId, ItemStoreSubOp.AssignToCharacter, w.ToArray()))
            {
                _log($"item AssignToCharacter item 0x{itemUid:X16}: master not connected; using local fallback");
                return _fallback.AssignToCharacter(itemUid, characterId, slotKind, slotPosition);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item AssignToCharacter item 0x{itemUid:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.AssignToCharacter(itemUid, characterId, slotKind, slotPosition);
        }
    }

    public bool Delete(ulong itemUid)
    {
        try
        {
            var w = new PacketWriter(8);
            w.WriteUInt64(itemUid);
            if (!FireAndForget(0, ItemStoreSubOp.Delete, w.ToArray()))
            {
                _log($"item Delete item 0x{itemUid:X16}: master not connected; using local fallback");
                return _fallback.Delete(itemUid);
            }
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item Delete item 0x{itemUid:X16}: master send failed ({ex.GetType().Name}); using local fallback");
            return _fallback.Delete(itemUid);
        }
    }

    public ushort AdjustStack(ulong itemUid, int delta)
    {
        try
        {
            var w = new PacketWriter(12);
            w.WriteUInt64(itemUid);
            w.WriteInt32(delta);
            var reply = Invoke(0, ItemStoreSubOp.AdjustStack, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"item AdjustStack item 0x{itemUid:X16}: master CANCEL; using local fallback");
                return _fallback.AdjustStack(itemUid, delta);
            }
            var reader = new PacketReader(reply);
            return reader.ReadUInt16();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item AdjustStack item 0x{itemUid:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.AdjustStack(itemUid, delta);
        }
    }

    public bool SetStack(ulong itemUid, ushort stack)
    {
        try
        {
            var w = new PacketWriter(10);
            w.WriteUInt64(itemUid);
            w.WriteUInt16(stack);
            var reply = Invoke(0, ItemStoreSubOp.SetStack, w.ToArray(), out var ok);
            if (!ok)
            {
                _log($"item SetStack item 0x{itemUid:X16}: master CANCEL; using local fallback");
                return _fallback.SetStack(itemUid, stack);
            }
            return reply.Length > 0 && reply[0] == 1;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log($"item SetStack item 0x{itemUid:X16}: master failed ({ex.GetType().Name}); using local fallback");
            return _fallback.SetStack(itemUid, stack);
        }
    }

    private byte[] Invoke(ulong charUid, ItemStoreSubOp subOp, byte[] body, out bool ok)
    {
        if (!_client.IsConnected)
        {
            ok = false;
            return Array.Empty<byte>();
        }

        var request = BuildRequest(charUid, subOp, body);
        var reply = _client
            .SendAndAwaitReplyAsync(request, AckOpcodes, _timeout, CancellationToken.None)
            .GetAwaiter().GetResult();
        ok = reply.Opcode == InterServerOpcode.TpItemStoreCallAckOk;
        return reply.Body;
    }

    private bool FireAndForget(ulong charUid, ItemStoreSubOp subOp, byte[] body)
    {
        if (!_client.IsConnected) return false;
        var request = BuildRequest(charUid, subOp, body);
        _client.SendAsync(request, CancellationToken.None).GetAwaiter().GetResult();
        return true;
    }

    private InterServerFrame BuildRequest(ulong charUid, ItemStoreSubOp subOp, byte[] body)
    {
        var fullBody = new byte[1 + body.Length];
        fullBody[0] = (byte)subOp;
        Buffer.BlockCopy(body, 0, fullBody, 1, body.Length);

        return new InterServerFrame
        {
            Opcode = InterServerOpcode.TpItemStoreCall,
            Header = new InterServerHeader
            {
                SourceServerIndex = _selfIndex,
                DestServerIndex = 0,
                CharUid = charUid,
                RequestId = _client.AllocateRequestId(),
            },
            Body = fullBody,
        };
    }
}
