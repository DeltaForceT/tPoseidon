using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class ItemThrowController
{
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly GroundItemField _groundItems;
    private readonly Func<ushort> _newEntityId;
    private readonly Func<ushort, Opcode, byte[], Task> _sendToMap;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _lock = new();

    public ItemThrowController(
        IItemStore items,
        IItemParamLookup catalog,
        GroundItemField groundItems,
        Func<ushort> newEntityId,
        Func<ushort, Opcode, byte[], Task> sendToMap,
        string logTag)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _groundItems = groundItems ?? throw new ArgumentNullException(nameof(groundItems));
        _newEntityId = newEntityId ?? throw new ArgumentNullException(nameof(newEntityId));
        _sendToMap = sendToMap ?? throw new ArgumentNullException(nameof(sendToMap));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task ThrowAsync(InGameSessionState thrower, ReadOnlyMemory<byte> body)
    {
        if (thrower.Character is not { } character) return;
        if (!ItemThrowRules.TryRead(body.Span, out var request))
        {
            await SendAsync(thrower, Opcode.S2C_ITEM_THROW_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        var held = _items.FindByUid(BagItemIds.Stored(request.WireUid));
        var keptInBag = _catalog.GetById((int)request.ItemId)?.IsKeptInBag ?? false;
        var outcome = ItemThrowRules.Judge(held, character.CharacterId, request.Count, keptInBag);
        if (outcome == ThrowOutcome.Ignored)
        {
            Console.WriteLine($"{_logTag} [items] 0x{thrower.PlayerEntityId:X4} cannot throw item {request.ItemId}: it stays in the bag");
            return;
        }
        if (outcome == ThrowOutcome.Refused || held is null)
        {
            Console.WriteLine($"{_logTag} [items] 0x{thrower.PlayerEntityId:X4} throw of item {request.ItemId} uid 0x{request.WireUid:X} x{request.Count} refused");
            await SendAsync(thrower, Opcode.S2C_ITEM_THROW_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }

        BagOperation operation;
        var rare = held.ItemClass == Item.RareClass;
        if (outcome == ThrowOutcome.PartOfStack)
        {
            var left = _items.AdjustStack(held.ItemUid, -request.Count);
            operation = new BagOperation(QuestResultFrames.StackChanged, held.ItemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left, held.ItemSlot);
        }
        else
        {
            var removed = rare ? _items.UnassignFromCharacter(held.ItemUid) : _items.Delete(held.ItemUid);
            if (!removed)
            {
                await SendAsync(thrower, Opcode.S2C_ITEM_THROW_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
                return;
            }
            operation = new BagOperation(QuestResultFrames.Removed, held.ItemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), held.Stack, held.ItemSlot);
        }
        await SendAsync(thrower, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(operation)).ConfigureAwait(false);

        var (x, y) = ItemThrowRules.Landing(thrower.PlayerX, thrower.PlayerY, request.X, request.Y);
        var item = new FieldItem(
            _newEntityId(),
            thrower.MapId,
            x,
            y,
            held.ItemIndex,
            rare ? held.Stack : request.Count,
            held.ItemClass,
            IsMoney: false,
            thrower.PlayerEntityId,
            FieldItem.NoOwner,
            DateTimeOffset.UtcNow)
        {
            ItemUid = rare ? held.ItemUid : 0,
        };
        _groundItems.Add(item);
        await _sendToMap(thrower.MapId, Opcode.S2C_ITEM_THROWN, GroundItemFrames.Thrown(item, thrower.PlayerX, thrower.PlayerY)).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [items] 0x{thrower.PlayerEntityId:X4} throws item {held.ItemIndex} x{item.Count} as 0x{item.EntityId:X4} at ({x},{y})");
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_lock) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
