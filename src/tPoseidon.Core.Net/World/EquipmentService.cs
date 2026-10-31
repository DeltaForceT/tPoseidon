using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Core.Net.World;

public sealed record EquipmentPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<ushort> EntityId { get; init; }
    public required Func<ushort> MapId { get; init; }
    public required Func<IReadOnlyList<int>> StatTotals { get; init; }
    public required Func<ushort, Opcode, byte[], Task> SendToMap { get; init; }
    public required Func<Task> EquipmentChanged { get; init; }
    public required Func<int, bool> HoldsAbility { get; init; }
}

public sealed class EquipmentService
{
    private readonly S2SessionContext _ctx;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly EquipmentPlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;

    public EquipmentService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        EquipmentPlayer player,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task EquipAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!EquipmentFrames.TryReadRequest(payload.Span, out var wireUid, out var slot))
        {
            Console.Error.WriteLine($"{_logTag} {_ctx.Remote} C2S 0x0051 is {payload.Length} bytes, {EquipmentFrames.RequestSize} expected; drop");
            return;
        }
        if (_player.Character() is not { } character) return;

        var uid = BagItemIds.Stored(wireUid);
        var item = _items.FindByUid(uid);
        if (item is null
            || item.CharacterId != character.CharacterId
            || (item.SlotKind != SlotKind.Inventory && item.SlotKind != SlotKind.Equipment)
            || _catalog.GetById(item.ItemIndex) is not { } definition)
        {
            await RefuseEquipAsync(wireUid, slot, "no such item in the bag", ct).ConfigureAwait(false);
            return;
        }

        if (TimedItemRules.HasExpired(item, DateTimeOffset.UtcNow))
        {
            await RefuseEquipAsync(wireUid, slot, $"item {item.ItemIndex}: the timed item has expired", ct).ConfigureAwait(false);
            return;
        }

        var worn = EquipmentRules.WornBy(_items, _catalog, character.CharacterId);
        if (EquipmentRules.CheckEquip(definition, item, slot, worn, character.Level, tPoseidon.Core.Game.Characters.LookChangeRules.BodyTypeOf(character), _player.StatTotals(), _player.HoldsAbility) is { } refusal)
        {
            await RefuseEquipAsync(wireUid, slot, $"item {item.ItemIndex}: {refusal}", ct).ConfigureAwait(false);
            return;
        }
        if (EquipmentRules.ReleasesBeforeEquip(definition, slot, worn) is not { } releases)
        {
            await RefuseEquipAsync(wireUid, slot, $"item {item.ItemIndex}: {EquipRefusal.OccupantInWrongSlot}", ct).ConfigureAwait(false);
            return;
        }

        var moved = new List<WornItem>(releases.Count);
        foreach (var release in releases)
        {
            if (!MoveToBag(character.CharacterId, release.Item.ItemUid))
            {
                Restore(moved);
                await RefuseEquipAsync(wireUid, slot, $"bag move of 0x{release.Item.ItemUid:X} failed", ct).ConfigureAwait(false);
                return;
            }
            moved.Add(release);
        }
        if (!_items.MoveToSlot(uid, SlotKind.Equipment, slot))
        {
            Restore(moved);
            await RefuseEquipAsync(wireUid, slot, $"equipment move of 0x{uid:X} failed", ct).ConfigureAwait(false);
            return;
        }

        foreach (var release in moved) await SendReleasedAsync(release, ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_EQUIP_ITEM_ACK, EquipmentFrames.ItemInSlot(BagItemIds.OnWire(uid), slot), ct).ConfigureAwait(false);
        await _player.SendToMap(_player.MapId(), Opcode.S2C_LOOK_ITEM_ADDED,
            EquipmentFrames.LookAdded(_player.EntityId(), (uint)item.ItemIndex, (uint)item.ItemIndex)).ConfigureAwait(false);
        await _player.EquipmentChanged().ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   equipped item {item.ItemIndex} (0x{uid:X}) in slot {slot}" +
            (moved.Count == 0 ? string.Empty : $", released {string.Join(", ", moved.Select(release => $"{release.Item.ItemIndex} from slot {release.Slot}"))}"));
    }

    public async Task ReleaseAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!EquipmentFrames.TryReadRequest(payload.Span, out var wireUid, out var slot))
        {
            Console.Error.WriteLine($"{_logTag} {_ctx.Remote} C2S 0x0054 is {payload.Length} bytes, {EquipmentFrames.RequestSize} expected; drop");
            return;
        }
        if (_player.Character() is not { } character) return;

        var uid = BagItemIds.Stored(wireUid);
        var worn = EquipmentRules.WornBy(_items, _catalog, character.CharacterId);
        if (worn.FirstOrDefault(held => held.Item.ItemUid == uid) is not { } target)
        {
            await RefuseReleaseAsync(wireUid, "the item is not worn", ct).ConfigureAwait(false);
            return;
        }
        if (EquipmentRules.CheckRelease(target, slot, worn, character.Level) is { } refusal)
        {
            await RefuseReleaseAsync(wireUid, $"item {target.Item.ItemIndex}: {refusal}", ct).ConfigureAwait(false);
            return;
        }
        if (!MoveToBag(character.CharacterId, uid))
        {
            await RefuseReleaseAsync(wireUid, $"bag move of 0x{uid:X} failed", ct).ConfigureAwait(false);
            return;
        }

        await SendReleasedAsync(target, ct).ConfigureAwait(false);
        await _player.EquipmentChanged().ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   released item {target.Item.ItemIndex} (0x{uid:X}) from slot {target.Slot}");
    }

    public async Task ReleaseListAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!EquipmentFrames.TryReadReleaseList(payload.Span, out var entries))
        {
            Console.Error.WriteLine($"{_logTag} {_ctx.Remote} C2S 0x02D1 is malformed ({payload.Length} bytes); refused");
            await RefuseReleaseAsync(0, "malformed release list", ct).ConfigureAwait(false);
            return;
        }
        if (_player.Character() is not { } character) return;

        var worn = EquipmentRules.WornBy(_items, _catalog, character.CharacterId);
        var plan = ReleaseListRules.Plan(entries, worn, character.Level);
        if (plan.Refusal is not null)
        {
            await RefuseReleaseAsync(0, plan.Refusal, ct).ConfigureAwait(false);
            return;
        }

        var moved = new List<WornItem>(plan.Items.Count);
        foreach (var target in plan.Items)
        {
            if (!MoveToBag(character.CharacterId, target.Item.ItemUid))
            {
                Restore(moved);
                await RefuseReleaseAsync(0, $"bag move of 0x{target.Item.ItemUid:X} failed", ct).ConfigureAwait(false);
                return;
            }
            moved.Add(target);
        }
        foreach (var target in moved) await SendReleasedAsync(target, ct).ConfigureAwait(false);
        if (moved.Count > 0) await _player.EquipmentChanged().ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   released {moved.Count} worn items at once");
    }

    public async Task EquipCommonAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!EquipmentFrames.TryReadRequest(payload.Span, out var wireUid, out var slot))
        {
            Console.Error.WriteLine($"{_logTag} {_ctx.Remote} C2S 0x0205 is {payload.Length} bytes, {EquipmentFrames.RequestSize} expected; drop");
            return;
        }
        if (_player.Character() is not { } character) return;

        var uid = BagItemIds.Stored(wireUid);
        var item = _items.FindByUid(uid);
        if (item is null
            || item.CharacterId != character.CharacterId
            || (item.SlotKind != SlotKind.Inventory && item.SlotKind != SlotKind.Equipment)
            || _catalog.GetById(item.ItemIndex) is not { } definition)
        {
            await RefuseCommonAsync(wireUid, "no such item in the bag", ct).ConfigureAwait(false);
            return;
        }
        if (definition.ObjectClass != EquipmentRules.CommonClass)
        {
            await RefuseCommonAsync(wireUid, $"item {item.ItemIndex}: class {definition.Class} is not common", ct).ConfigureAwait(false);
            return;
        }
        if (TimedItemRules.HasExpired(item, DateTimeOffset.UtcNow))
        {
            await RefuseCommonEquipAsync(wireUid, $"item {item.ItemIndex}: the timed item has expired", ct).ConfigureAwait(false);
            return;
        }

        var worn = EquipmentRules.WornBy(_items, _catalog, character.CharacterId);
        if (EquipmentRules.CheckEquipCommon(definition, item, worn, character.Level, tPoseidon.Core.Game.Characters.LookChangeRules.BodyTypeOf(character), _player.StatTotals()) is { } refusal)
        {
            await RefuseCommonEquipAsync(wireUid, $"item {item.ItemIndex}: {refusal}", ct).ConfigureAwait(false);
            return;
        }
        if (EquipmentRules.ReleasesBeforeEquipCommon(definition, worn) is not { } releases)
        {
            await RefuseCommonEquipAsync(wireUid, $"item {item.ItemIndex}: the worn gun or card cannot be released", ct).ConfigureAwait(false);
            return;
        }

        var moved = new List<WornItem>(releases.Count);
        foreach (var release in releases)
        {
            if (!MoveToBag(character.CharacterId, release.Item.ItemUid))
            {
                Restore(moved);
                await RefuseCommonEquipAsync(wireUid, $"bag move of 0x{release.Item.ItemUid:X} failed", ct).ConfigureAwait(false);
                return;
            }
            moved.Add(release);
        }
        if (!_items.MoveToSlot(uid, SlotKind.Equipment, EquipmentSlot.Common))
        {
            Restore(moved);
            await RefuseCommonEquipAsync(wireUid, $"equipment move of 0x{uid:X} failed", ct).ConfigureAwait(false);
            return;
        }

        foreach (var release in moved) await SendReleasedAsync(release, ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_COMMON_ITEM_EQUIPPED, EquipmentFrames.ItemInSlot(BagItemIds.OnWire(uid), slot), ct).ConfigureAwait(false);
        await _player.EquipmentChanged().ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   loaded item {item.ItemIndex} (0x{uid:X}) in the common slot" +
            (moved.Count == 0 ? string.Empty : $", released {string.Join(", ", moved.Select(release => $"{release.Item.ItemIndex} from slot {release.Slot}"))}"));
    }

    public async Task ReleaseCommonAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!EquipmentFrames.TryReadRequest(payload.Span, out var wireUid, out var slot))
        {
            Console.Error.WriteLine($"{_logTag} {_ctx.Remote} C2S 0x0208 is {payload.Length} bytes, {EquipmentFrames.RequestSize} expected; drop");
            return;
        }
        if (_player.Character() is not { } character) return;

        var uid = BagItemIds.Stored(wireUid);
        var item = _items.FindByUid(uid);
        if (item is null
            || item.CharacterId != character.CharacterId
            || (item.SlotKind != SlotKind.Inventory && item.SlotKind != SlotKind.Equipment)
            || _catalog.GetById(item.ItemIndex) is not { } definition
            || definition.ObjectClass != EquipmentRules.CommonClass)
        {
            await RefuseCommonAsync(wireUid, "no such common item in the bag", ct).ConfigureAwait(false);
            return;
        }
        var worn = EquipmentRules.WornBy(_items, _catalog, character.CharacterId);
        if (EquipmentRules.CheckReleaseCommon(definition, item, slot, worn) is { } refusal)
        {
            await RefuseCommonAsync(wireUid, $"item {item.ItemIndex}: {refusal}", ct).ConfigureAwait(false);
            return;
        }
        if (!MoveToBag(character.CharacterId, uid))
        {
            await RefuseCommonAsync(wireUid, $"bag move of 0x{uid:X} failed", ct).ConfigureAwait(false);
            return;
        }

        await SendCommonReleasedAsync(uid, ct).ConfigureAwait(false);
        await _player.EquipmentChanged().ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   unloaded item {item.ItemIndex} (0x{uid:X}) from the common slot");
    }

    public async Task ReleaseExpiredAsync(ulong itemUid, CancellationToken ct)
    {
        if (_player.Character() is not { } character) return;
        var worn = EquipmentRules.WornBy(_items, _catalog, character.CharacterId);
        if (worn.FirstOrDefault(held => held.Item.ItemUid == itemUid) is not { } target) return;
        if (!MoveToBag(character.CharacterId, itemUid))
        {
            Console.Error.WriteLine($"{_logTag} {_ctx.Remote}   expired item 0x{itemUid:X} could not move to the bag; it stays equipped");
            return;
        }
        await SendReleasedAsync(target, ct).ConfigureAwait(false);
        await _player.EquipmentChanged().ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   expired item {target.Item.ItemIndex} (0x{itemUid:X}) released from slot {target.Slot}");
    }

    public async Task<int> ReleaseAllAsync(CancellationToken ct)
    {
        if (_player.Character() is not { } character) return 0;
        var released = 0;
        foreach (var worn in EquipmentRules.WornBy(_items, _catalog, character.CharacterId))
        {
            if (!MoveToBag(character.CharacterId, worn.Item.ItemUid))
            {
                Console.Error.WriteLine($"{_logTag} {_ctx.Remote}   could not move item 0x{worn.Item.ItemUid:X} from slot {worn.Slot} to the bag");
                continue;
            }
            await SendReleasedAsync(worn, ct).ConfigureAwait(false);
            released++;
        }
        if (released > 0) await _player.EquipmentChanged().ConfigureAwait(false);
        return released;
    }

    private bool MoveToBag(ulong characterId, ulong itemUid)
    {
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        return _items.MoveToSlot(itemUid, SlotKind.Inventory, EquipmentRules.FirstFreeBagPosition(bag));
    }

    private void Restore(IReadOnlyList<WornItem> moved)
    {
        foreach (var release in moved)
        {
            if (!_items.MoveToSlot(release.Item.ItemUid, SlotKind.Equipment, release.Slot))
            {
                Console.Error.WriteLine($"{_logTag} {_ctx.Remote}   could not put item 0x{release.Item.ItemUid:X} back in slot {release.Slot}");
            }
        }
    }

    private async Task SendReleasedAsync(WornItem release, CancellationToken ct)
    {
        if (release.Slot == EquipmentSlot.Common)
        {
            await SendCommonReleasedAsync(release.Item.ItemUid, ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_ITEM_RELEASED, EquipmentFrames.ItemInSlot(BagItemIds.OnWire(release.Item.ItemUid), release.Slot), ct).ConfigureAwait(false);
        await _player.SendToMap(_player.MapId(), Opcode.S2C_LOOK_ITEM_REMOVED,
            EquipmentFrames.LookRemoved(_player.EntityId(), (uint)release.Item.ItemIndex)).ConfigureAwait(false);
    }

    private Task SendCommonReleasedAsync(ulong itemUid, CancellationToken ct) =>
        SendAsync(Opcode.S2C_COMMON_ITEM_RELEASED, EquipmentFrames.ItemInSlot(BagItemIds.OnWire(itemUid), EquipmentSlot.Common), ct);

    private async Task RefuseCommonEquipAsync(ulong wireUid, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   common equip of 0x{wireUid:X} refused: {reason}");
        await SendAsync(Opcode.S2C_COMMON_EQUIP_REFUSED, EquipmentFrames.Refused(wireUid), ct).ConfigureAwait(false);
    }

    private async Task RefuseCommonAsync(ulong wireUid, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   common item 0x{wireUid:X} refused: {reason}");
        await SendAsync(Opcode.S2C_COMMON_ITEM_REFUSED, EquipmentFrames.Refused(wireUid), ct).ConfigureAwait(false);
    }

    private async Task RefuseEquipAsync(ulong wireUid, byte slot, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   equip of 0x{wireUid:X} in slot {slot} refused: {reason}");
        await SendAsync(Opcode.S2C_EQUIP_REFUSED, EquipmentFrames.Refused(wireUid), ct).ConfigureAwait(false);
    }

    private async Task RefuseReleaseAsync(ulong wireUid, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   release of 0x{wireUid:X} refused: {reason}");
        await SendAsync(Opcode.S2C_RELEASE_REFUSED, EquipmentFrames.Refused(wireUid), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
