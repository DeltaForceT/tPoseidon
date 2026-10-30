using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record RefinePlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public Func<int> Fortune { get; init; } = () => 0;
}

public sealed class RefineService
{
    public const int RequestSize = 28;
    public const ushort PhantomOpened = 0x05A8;
    public const ushort Repair = 0x05A9;
    public const ushort Repaired = 0x05AA;
    public const ushort RepairRefused = 0x05AB;
    public const ushort PhantomClose = 0x05AC;
    public const ushort PhantomClosed = 0x05AD;
    public const int RepairRequestSize = 8;

    private readonly S2SessionContext _ctx;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly ItemPropertyRules _rules;
    private readonly IItemPropertyStore _properties;
    private readonly IItemRefineStore _refines;
    private readonly RefineTables _tables;
    private readonly RefinePlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;

    public RefineService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        ItemPropertyRules rules,
        IItemPropertyStore properties,
        IItemRefineStore refines,
        RefineTables tables,
        RefinePlayer player,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _refines = refines ?? throw new ArgumentNullException(nameof(refines));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsOpen { get; private set; }

    public bool Phantom { get; private set; }

    public void Leave()
    {
        IsOpen = false;
        Phantom = false;
    }

    public async Task<int> OpenAsync(CancellationToken ct)
    {
        IsOpen = true;
        Phantom = false;
        await SendAsync(Opcode.S2C_REFINE_OPENED, Array.Empty<byte>(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   refine window opened");
        return NpcScript.Silent;
    }

    public async Task<int> OpenPhantomAsync(CancellationToken ct)
    {
        IsOpen = true;
        Phantom = true;
        await _ctx.SendFrameAsync(PhantomOpened, Array.Empty<byte>(), randKey: NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   phantom academy refine window opened");
        return NpcScript.Silent;
    }

    public async Task ClosePhantomAsync(CancellationToken ct)
    {
        Leave();
        await _ctx.SendFrameAsync(PhantomClosed, Array.Empty<byte>(), randKey: NextRandKey(), ct).ConfigureAwait(false);
    }

    public async Task RepairAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || _player.Character() is not { } character)
        {
            await RepairRefusedAsync(RepairRules.InvalidPlayerState, "the refine window is not open", ct).ConfigureAwait(false);
            return;
        }
        if (payload.Length < RepairRequestSize)
        {
            await RepairRefusedAsync(RepairRules.UnknownError, $"request of {payload.Length} bytes", ct).ConfigureAwait(false);
            return;
        }
        var targetUid = BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload.Span));
        if (_items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Equipment).Any(item => item.ItemUid == targetUid))
        {
            await RepairRefusedAsync(RepairRules.EquippedItem, $"item 0x{targetUid:X} is worn", ct).ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var target = bag.FirstOrDefault(item => item.ItemUid == targetUid);
        var definition = target is null ? null : _catalog.GetById(target.ItemIndex);
        if (target is null || definition is null || RepairRules.Check(definition, target, Phantom) != RepairRules.NoError
            || RepairRules.Material(_tables, definition, target) is not { } repairRow)
        {
            await RepairRefusedAsync(RepairRules.ImpossibleItem, $"item 0x{targetUid:X} cannot be repaired here", ct).ConfigureAwait(false);
            return;
        }
        var count = repairRow.RepairCount;
        var material = bag.FirstOrDefault(item => item.ItemIndex == repairRow.RepairItemId && item.Stack >= count);
        if (material is null)
        {
            await RepairRefusedAsync(RepairRules.NoMaterial, $"no item {repairRow.RepairItemId} x{count} for item {target.ItemIndex}", ct).ConfigureAwait(false);
            return;
        }
        var materialOperation = Take(material, count);
        _refines.SetRefine(target.ItemUid, target.RefineLevel, RepairRules.Repaired);
        var repaired = new BagOperation(QuestResultFrames.StackChanged, ItemParamEntry.RareClass, (uint)target.ItemIndex, BagItemIds.OnWire(target.ItemUid),
            target.Stack, target.ItemSlot, (byte)definition.RefineType, target.RefineLevel, RepairRules.Repaired);
        await _ctx.SendFrameAsync(Repaired, QuestResultFrames.ItemOperations(new[] { materialOperation, repaired }), randKey: NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   repair item {target.ItemIndex} level {target.RefineLevel} with {count} x {material.ItemIndex}{(Phantom ? " (phantom academy)" : string.Empty)}");
    }

    private async Task RepairRefusedAsync(byte code, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   repair refused ({code}): {reason}");
        await _ctx.SendFrameAsync(RepairRefused, new[] { code }, randKey: NextRandKey(), ct).ConfigureAwait(false);
    }

    public async Task RefineAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || _player.Character() is not { } character || _player.Stats() is not { } stats) return;
        if (payload.Length < RequestSize)
        {
            await RefuseAsync($"request of {payload.Length} bytes", ct).ConfigureAwait(false);
            return;
        }
        var (targetUid, materialUid, supportUid, supportRowId) = ReadRequest(payload.Span);

        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var target = bag.FirstOrDefault(item => item.ItemUid == targetUid);
        if (target is null || _catalog.GetById(target.ItemIndex) is not { ObjectClass: ItemParamEntry.RareClass } definition)
        {
            await RefuseAsync($"target 0x{targetUid:X} is not an equipment item in the bag", ct).ConfigureAwait(false);
            return;
        }
        var propertyId = RefineRules.PropertyIdFor(definition.RefineType);
        var baseProperty = propertyId is { } id ? PropertiesOf(target, definition).FirstOrDefault(p => p.Type == ItemPropertyTypes.Stat && p.Id == id) : default;
        if (propertyId is null || baseProperty.Id == 0)
        {
            await RefuseAsync($"item {target.ItemIndex} has no base property for refine type {definition.RefineType}", ct).ConfigureAwait(false);
            return;
        }
        var level = (int)target.RefineLevel;
        var fee = RefineRules.Fee(definition, level, _tables.CostDiscount);
        if (stats.Money < fee || !RefineRules.CanRefine(definition, target))
        {
            await RefuseAsync($"item {target.ItemIndex} level {level} state {target.RefineState}: cannot be refined (fee {fee}, money {stats.Money})", ct).ConfigureAwait(false);
            return;
        }
        var materialRow = RefineRules.MaterialRow(_tables, definition.RefineType, definition.MinLevel);
        var material = bag.FirstOrDefault(item => item.ItemUid == materialUid);
        if (materialRow is null || material is null
            || material.ItemIndex != materialRow.ItemIds[level]
            || material.Stack < materialRow.ItemCounts[level])
        {
            await RefuseAsync($"material for item {target.ItemIndex} level {level + 1} is missing", ct).ConfigureAwait(false);
            return;
        }
        if (!_tables.LevelRows.TryGetValue(definition.RefineIndex, out var levelRow))
        {
            await RefuseAsync($"no RefineLevelTable row {definition.RefineIndex}", ct).ConfigureAwait(false);
            return;
        }
        RefineSupportRow? supportRow = null;
        Item? support = null;
        if (supportRowId != 0)
        {
            support = bag.FirstOrDefault(item => item.ItemUid == supportUid);
            if (support is not null && _tables.Supports.TryGetValue(supportRowId, out var row) && row.ItemId == support.ItemIndex) supportRow = row;
            else
            {
                support = null;
                Console.WriteLine($"{_logTag} {_ctx.Remote}   refine: support row {supportRowId} does not match item 0x{supportUid:X}; refining without it");
            }
        }

        var chance = RefineRules.Chance(levelRow.Ratio[level], _player.Fortune(), _tables.RefineSeed, supportRow?.Prob ?? 0);
        bool success;
        lock (_rng) success = _rng.Next(RefineRules.RollRange) < chance;

        stats.GrantMoney(-(int)fee, StatChangeSource.Refine);
        var materialOperation = Take(material, materialRow.ItemCounts[level]);
        var supportOperation = support is not null && supportRow is not null
            ? Take(support, supportRow.ItemCount)
            : new BagOperation(0, ItemParamEntry.StackableClass, 0, 0, 0);
        short value = 0;
        if (success)
        {
            value = RefineRules.Bonus(baseProperty.Value, levelRow.Accumulation[level]);
            var stored = _properties.ForItem(target.ItemUid)
                .Where(p => !(p.Type == ItemPropertyTypes.RefineBonus && p.Id == propertyId.Value))
                .Append(new ItemProperty(ItemPropertyTypes.RefineBonus, propertyId.Value, value, 0))
                .ToList();
            _properties.Replace(target.ItemUid, stored);
            _refines.SetRefine(target.ItemUid, (byte)(level + 1), target.RefineState);
        }
        else
        {
            _refines.SetRefine(target.ItemUid, (byte)level, RefineRules.Broken);
        }

        var w = new PacketWriter(64);
        w.WriteByte(success ? (byte)1 : (byte)0);
        w.WriteBytes(QuestResultFrames.ItemOperation(materialOperation));
        w.WriteBytes(QuestResultFrames.ItemOperation(supportOperation));
        w.WriteUInt64(BagItemIds.OnWire(target.ItemUid));
        w.WriteUInt16((ushort)propertyId.Value);
        w.WriteUInt16((ushort)value);
        w.WriteUInt32Field(stats.Money);
        await SendAsync(Opcode.S2C_REFINE_RESULT, w.ToArray(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   refine item {target.ItemIndex} level {level} -> {(success ? level + 1 : level)}: {(success ? $"success, property {propertyId} = {value}" : "failed, item broken")} (chance {chance / 100:F2}%, fee {fee})");
    }

    public static (ulong Target, ulong Material, ulong Support, int SupportRow) ReadRequest(ReadOnlySpan<byte> payload) => (
        BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload)),
        BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload[8..])),
        BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload[16..])),
        (int)BinaryPrimitives.ReadUInt32LittleEndian(payload[24..]));

    private IEnumerable<ItemProperty> PropertiesOf(Item item, ItemParamEntry definition) =>
        _properties.ForItem(item.ItemUid).Concat(_rules.Defaults(definition));

    private BagOperation Take(Item held, int count)
    {
        var itemClass = _catalog.GetById(held.ItemIndex)?.ObjectClass ?? held.ItemClass;
        if (held.Stack <= count)
        {
            _items.Delete(held.ItemUid);
            return new BagOperation(QuestResultFrames.Removed, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), 0);
        }
        var left = _items.AdjustStack(held.ItemUid, -count);
        return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)held.ItemIndex, BagItemIds.OnWire(held.ItemUid), left);
    }

    private async Task RefuseAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   refine refused: {reason}");
        await SendAsync(Opcode.S2C_REFINE_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
