using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Warp;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Dialogs;
using tPoseidon.Core.Net.World;

internal sealed class MemoryPortController
{
    private readonly MasterMemoryPorts _master;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly Func<IReadOnlyDictionary<int, MemoryPortEntry>> _rows;
    private readonly Func<ushort, int> _mapOption;
    private readonly Func<InGameSessionState, (int X, int Y)> _position;
    private readonly Encoding _text;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public MemoryPortController(
        MasterMemoryPorts master,
        IItemStore items,
        IItemParamLookup catalog,
        Func<IReadOnlyDictionary<int, MemoryPortEntry>> rows,
        Func<ushort, int> mapOption,
        Func<InGameSessionState, (int X, int Y)> position,
        Encoding text,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _rows = rows ?? throw new ArgumentNullException(nameof(rows));
        _mapOption = mapOption ?? throw new ArgumentNullException(nameof(mapOption));
        _position = position ?? throw new ArgumentNullException(nameof(position));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task OpenAsync(InGameSessionState player, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (player.Character is not { } character) return;
        if (!MemoryPortFrames.TryReadOpen(payload.Span, out var wireUid) || RowOf(character.CharacterId, wireUid) is not { } row)
        {
            Log(player, "open refused: invalid memoryport info");
            await SendAsync(player, Opcode.S2C_MEMORY_PORT_OPEN_FAILED, MemoryPortFrames.OpenFailed(MemoryPortRules.UndeterminedErrorCode)).ConfigureAwait(false);
            return;
        }
        var opened = await _master.OpenAsync(character.CharacterId, (byte)row.Type, ct).ConfigureAwait(false);
        if (opened is not { Slots: { } slots })
        {
            Log(player, $"open of item {row.ItemId} failed (code {opened?.Code.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none"})");
            await SendAsync(player, Opcode.S2C_MEMORY_PORT_OPEN_FAILED, MemoryPortFrames.OpenFailed(opened?.Code ?? MemoryPortRules.UndeterminedErrorCode)).ConfigureAwait(false);
            return;
        }
        await SendAsync(player, Opcode.S2C_MEMORY_PORT_LIST, MemoryPortFrames.List(wireUid, slots, _text)).ConfigureAwait(false);
        Log(player, $"opens item {row.ItemId} (type {row.Type}, {row.SlotCount} slots): {slots.Count} saved");
    }

    public async Task SaveAsync(InGameSessionState player, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (player.Character is not { } character) return;
        if (!MemoryPortFrames.TryReadSave(payload.Span, _text, out var save) || RowOf(character.CharacterId, save.WireItemUid) is not { } row)
        {
            await RefuseSaveAsync(player, "invalid memoryport info").ConfigureAwait(false);
            return;
        }
        if (MemoryPortRules.RefusesSave(_mapOption(player.MapId)))
        {
            await RefuseSaveAsync(player, $"map {player.MapId} refuses saving").ConfigureAwait(false);
            return;
        }
        if (save.Slot > byte.MaxValue)
        {
            await RefuseSaveAsync(player, $"slot {save.Slot} out of range").ConfigureAwait(false);
            return;
        }
        var (x, y) = _position(player);
        var slot = new MemoryPort
        {
            CharacterId = character.CharacterId,
            PortType = (byte)row.Type,
            SlotNum = (byte)save.Slot,
            Zone = (short)player.MapId,
            PositionX = x,
            PositionY = y,
            Description = MemoryPortFrames.StoredDescription(save.Description, _text),
        };
        if (!await _master.AddAsync(character.CharacterId, slot, ct).ConfigureAwait(false))
        {
            await RefuseSaveAsync(player, $"slot {save.Slot} not stored").ConfigureAwait(false);
            return;
        }
        await SendAsync(player, Opcode.S2C_MEMORY_PORT_SAVED, MemoryPortFrames.Record(slot, _text)).ConfigureAwait(false);
        Log(player, $"saves slot {save.Slot}: map {player.MapId} ({x},{y})");
    }

    public async Task DeleteAsync(InGameSessionState player, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (player.Character is not { } character) return;
        if (!MemoryPortFrames.TryReadSlot(payload.Span, out var wireUid, out var slot) || RowOf(character.CharacterId, wireUid) is not { } row)
        {
            Log(player, "delete refused: invalid memoryport info");
            await SendAsync(player, Opcode.S2C_MEMORY_PORT_DELETE_FAILED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        if (!await _master.DeleteAsync(character.CharacterId, new MemoryPortSlotRequest((byte)row.Type, slot), ct).ConfigureAwait(false))
        {
            Log(player, $"delete of slot {slot} failed");
            await SendAsync(player, Opcode.S2C_MEMORY_PORT_DELETE_FAILED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        await SendAsync(player, Opcode.S2C_MEMORY_PORT_DELETED, MemoryPortFrames.Deleted(slot)).ConfigureAwait(false);
        Log(player, $"deletes slot {slot}");
    }

    public async Task<bool> TryUseAsync(InGameSessionState player, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (player.Character is not { } character || player.Stats is not { } stats || player.IsDead) return false;
        if (!MemoryPortFrames.TryReadUseWithData(payload.Span, out var use)) return false;
        var uid = BagItemIds.Stored(use.WireItemUid);
        if (BagItem(character.CharacterId, uid) is not { } item || _catalog.GetById(item.ItemIndex) is not { } definition) return false;
        if (ItemUseRules.HelperOf(definition.Type) != ItemUseHelper.Use || !MemoryPortRules.IsMemoryPort(definition.Effect, definition.Options)) return false;
        if (ItemUseRules.Check(definition, stats.Level) is { } refusal)
        {
            Log(player, $"item {item.ItemIndex}: {refusal}");
            await SendAsync(player, Opcode.S2C_ITEM_USE_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return true;
        }
        if (MemoryPortRules.RefusesWarp(_mapOption(player.MapId)))
        {
            Log(player, $"teleport refused on map {player.MapId}");
            await SendAsync(player, Opcode.S2C_WARP_ITEM_REFUSED, MemoryPortFrames.WarpRefused(MemoryPortRules.WarpRefusedReason)).ConfigureAwait(false);
            return true;
        }
        if (use.Data.Length < MemoryPortRules.UseSlotDataBytes) return true;
        var slotNumber = BinaryPrimitives.ReadUInt16LittleEndian(use.Data);
        if (!_rows().TryGetValue(item.ItemIndex, out var row))
        {
            Log(player, $"hItemMemoryport::GetInfo() item {item.ItemIndex} has no row");
            return true;
        }
        var slot = await _master.UseAsync(character.CharacterId, new MemoryPortSlotRequest((byte)row.Type, slotNumber), ct).ConfigureAwait(false);
        if (slot is null)
        {
            Log(player, $"teleport to slot {slotNumber} cancelled by the Master");
            return true;
        }
        if (BagItem(character.CharacterId, uid) is not { } held)
        {
            Log(player, $"teleport to slot {slotNumber}: item 0x{uid:X} left the bag");
            return true;
        }
        await SendAsync(player, Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(use.WireItemUid)).ConfigureAwait(false);
        var left = TakeOne(held, definition);
        Log(player, $"teleports with item {held.ItemIndex} to slot {slotNumber}: map {slot.Zone} ({slot.PositionX},{slot.PositionY}), {left} left");
        if (player.RequestZoneChange is { } requestZoneChange)
        {
            await requestZoneChange(new ZoneChangeRequest(
                (ushort)slot.Zone,
                MemoryPortRules.ChangeZoneType,
                null,
                MemoryPortRules.WarpDelay,
                ((ushort)Math.Clamp(slot.PositionX, 0, ushort.MaxValue), (ushort)Math.Clamp(slot.PositionY, 0, ushort.MaxValue))), ct).ConfigureAwait(false);
        }
        return true;
    }

    private MemoryPortEntry? RowOf(ulong characterId, ulong wireUid) =>
        BagItem(characterId, BagItemIds.Stored(wireUid)) is { } item && _rows().TryGetValue(item.ItemIndex, out var row) ? row : null;

    private Item? BagItem(ulong characterId, ulong uid) =>
        _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(held => held.ItemUid == uid);

    private int TakeOne(Item item, ItemParamEntry definition)
    {
        if (definition.IsKeptWhenUsed) return item.Stack;
        if (item.Stack > 1) return _items.AdjustStack(item.ItemUid, -1);
        _items.Delete(item.ItemUid);
        return 0;
    }

    private async Task RefuseSaveAsync(InGameSessionState player, string reason)
    {
        Log(player, $"save refused: {reason}");
        await SendAsync(player, Opcode.S2C_MEMORY_PORT_SAVE_FAILED, Array.Empty<byte>()).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [memory port] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
