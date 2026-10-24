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

public sealed record GrowthPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public required Func<int> Luck { get; init; }
    public required Func<IReadOnlyDictionary<int, int>> Skills { get; init; }
}

public sealed class GrowthService
{
    public const int GrowthRequestSize = 32;
    public const int ResetRequestSize = 20;
    public const int Prop9Size = 9;

    private readonly S2SessionContext _ctx;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly ItemPropertyRules _rules;
    private readonly IItemPropertyStore _properties;
    private readonly GrowthTables _tables;
    private readonly GrowthPlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;

    public GrowthService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        ItemPropertyRules rules,
        IItemPropertyStore properties,
        GrowthTables tables,
        GrowthPlayer player,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsGrowthOpen { get; private set; }

    public bool IsResetOpen { get; private set; }

    public void Leave()
    {
        IsGrowthOpen = false;
        IsResetOpen = false;
    }

    public async Task<int> OpenAsync(bool reset, CancellationToken ct)
    {
        IsGrowthOpen = !reset;
        IsResetOpen = reset;
        await SendAsync(reset ? Opcode.S2C_RESET_MIX_OPENED : Opcode.S2C_GROWTH_OPENED, Array.Empty<byte>(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   {(reset ? "mix reset" : "maturing compound")} window opened");
        return NpcScript.Silent;
    }

    public async Task GrowAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsGrowthOpen || _player.Character() is not { } character || _player.Stats() is not { } stats) return;
        if (payload.Length < GrowthRequestSize)
        {
            await RefuseGrowthAsync($"request of {payload.Length} bytes", ct).ConfigureAwait(false);
            return;
        }
        var (targetUid, materialUid, firstAidUid, secondAidUid) = ReadGrowth(payload.Span);
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var target = bag.FirstOrDefault(item => item.ItemUid == targetUid);
        if (target is null || _catalog.GetById(target.ItemIndex) is not { ObjectClass: ItemParamEntry.RareClass } definition
            || !_tables.EquipProperties.TryGetValue(target.ItemIndex, out var equipRow))
        {
            await RefuseGrowthAsync($"target 0x{targetUid:X} is not a mixable equipment item in the bag", ct).ConfigureAwait(false);
            return;
        }
        var stored = _properties.ForItem(target.ItemUid);
        var held = _rules.Full(definition, stored);
        var slot = GrowthRules.FreeSlot(held, target.ItemSlot);
        if (slot == GrowthRules.NoSlot)
        {
            await RefuseGrowthAsync($"item {target.ItemIndex} has no free mix slot ({target.ItemSlot} slots, {held.Count} properties)", ct).ConfigureAwait(false);
            return;
        }
        if (target.RefineState == RefineRules.Broken && (definition.Options & GrowthRules.BrokenUsableOption) == 0)
        {
            await RefuseGrowthAsync($"item {target.ItemIndex} is broken", ct).ConfigureAwait(false);
            return;
        }
        var material = bag.FirstOrDefault(item => item.ItemUid == materialUid);
        if (material is null || !_tables.Materials.TryGetValue(material.ItemIndex, out var materialRow)
            || !equipRow.EnableProperties.Contains(materialRow.EnableProperty)
            || material.Stack < materialRow.ItemNum
            || definition.MinLevel < materialRow.EquipLevel)
        {
            await RefuseGrowthAsync($"material 0x{materialUid:X} does not fit item {target.ItemIndex}", ct).ConfigureAwait(false);
            return;
        }
        var aidItems = new[] { firstAidUid, secondAidUid }
            .Where(uid => uid != 0)
            .Select(uid => bag.FirstOrDefault(item => item.ItemUid == uid))
            .ToList();
        if (aidItems.Any(item => item is null))
        {
            await RefuseGrowthAsync("an aid item is not in the bag", ct).ConfigureAwait(false);
            return;
        }
        var aids = aidItems.Select(item => _tables.Aids.TryGetValue(item!.ItemIndex, out var help)
            ? new GrowthAid(help.ApplyRatio, help.MinAbility, help.MaxAbility)
            : default).ToList();
        if (aids.Count == 2 && GrowthRules.AidsClash(aids[0], aids[1]))
        {
            await RefuseGrowthAsync("the two aid items do the same thing", ct).ConfigureAwait(false);
            return;
        }
        var fee = GrowthRules.Fee(definition, _tables.CostDiscount);
        if (stats.Money < fee)
        {
            await RefuseGrowthAsync($"fee {fee}, money {stats.Money}", ct).ConfigureAwait(false);
            return;
        }

        var masterRatio = _player.Skills().TryGetValue(GrowthTables.CompoundMasterSkill, out var masterLevel)
            && _tables.CompoundMasterRatios.TryGetValue(masterLevel, out var ratio) ? ratio : 0f;
        var chance = GrowthRules.Chance(equipRow.ApplyRatio, aids, _player.Luck(), definition.MinLevel, masterRatio);
        var deltas = GrowthRules.Deltas(materialRow, aids);
        short ability;
        lock (_rng)
        {
            ability = _rng.Next(GrowthRules.ChanceRoll) < chance && _rules.CurveBin(materialRow.Curve, _rng) is { } bin
                ? GrowthRules.Ability(materialRow, deltas, bin)
                : GrowthRules.FailedAbility(materialRow, deltas);
        }

        stats.GrantMoney(-(int)fee, StatChangeSource.Growth);
        var materialOperation = Take(material, materialRow.ItemNum);
        var aidOperations = aidItems.Select(item => Take(item!, 1)).ToList();
        while (aidOperations.Count < 2) aidOperations.Add(new BagOperation(0, ItemParamEntry.StackableClass, 0, 0, 0));
        var element = new ItemProperty(slot, materialRow.EnableProperty, ability, (uint)material.ItemIndex);
        _properties.Replace(target.ItemUid, stored.Append(element).ToList());

        var w = new PacketWriter(96);
        w.WriteBytes(QuestResultFrames.ItemOperation(materialOperation));
        w.WriteUInt64(BagItemIds.OnWire(target.ItemUid));
        WriteProp9(w, element);
        w.WriteZero(Prop9Size);
        w.WriteUInt32Field(stats.Money);
        foreach (var operation in aidOperations) w.WriteBytes(QuestResultFrames.ItemOperation(operation));
        await SendAsync(Opcode.S2C_GROWTH_RESULT, w.ToArray(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   maturing compound item {target.ItemIndex} slot {slot}: property {materialRow.EnableProperty} = {ability} (chance {chance}%, fee {fee})");
    }

    public async Task ResetAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsResetOpen || _player.Character() is not { } character) return;
        if (payload.Length < ResetRequestSize)
        {
            await RefuseResetAsync($"request of {payload.Length} bytes", ct).ConfigureAwait(false);
            return;
        }
        var (targetUid, materialUid, slot) = ReadReset(payload.Span);
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var target = bag.FirstOrDefault(item => item.ItemUid == targetUid);
        var stored = target is null ? Array.Empty<ItemProperty>() : _properties.ForItem(target.ItemUid);
        if (target is null || _catalog.GetById(target.ItemIndex) is not { ObjectClass: ItemParamEntry.RareClass }
            || !_tables.EquipProperties.ContainsKey(target.ItemIndex)
            || stored.Count == 0
            || !stored.Any(p => p.Type == slot && slot is >= 0 and <= ItemPropertyTypes.LastMixSlot))
        {
            await RefuseResetAsync($"target 0x{targetUid:X} has no mix in slot {slot}", ct).ConfigureAwait(false);
            return;
        }
        var material = bag.FirstOrDefault(item => item.ItemUid == materialUid);
        if (material is null || material.ItemIndex != GrowthTables.ResetMaterialItemId)
        {
            await RefuseResetAsync($"material 0x{materialUid:X} is not item {GrowthTables.ResetMaterialItemId}", ct).ConfigureAwait(false);
            return;
        }
        var materialOperation = Take(material, 1);
        _properties.Replace(target.ItemUid, stored.Where(p => p.Type != slot).ToList());

        var w = new PacketWriter(40);
        w.WriteUInt64(BagItemIds.OnWire(target.ItemUid));
        w.WriteUInt32((uint)slot);
        w.WriteUInt32(0);
        w.WriteBytes(QuestResultFrames.ItemOperation(materialOperation));
        await SendAsync(Opcode.S2C_RESET_MIX_RESULT, w.ToArray(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   mix reset item {target.ItemIndex} slot {slot}");
    }

    public static (ulong Target, ulong Material, ulong FirstAid, ulong SecondAid) ReadGrowth(ReadOnlySpan<byte> payload) => (
        BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload)),
        BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload[8..])),
        Optional(BinaryPrimitives.ReadUInt64LittleEndian(payload[16..])),
        Optional(BinaryPrimitives.ReadUInt64LittleEndian(payload[24..])));

    public static (ulong Target, ulong Material, int Slot) ReadReset(ReadOnlySpan<byte> payload) => (
        BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload)),
        BagItemIds.Stored(BinaryPrimitives.ReadUInt64LittleEndian(payload[8..])),
        (int)BinaryPrimitives.ReadUInt32LittleEndian(payload[16..]));

    public static void WriteProp9(PacketWriter w, ItemProperty property)
    {
        ArgumentNullException.ThrowIfNull(w);
        w.WriteByte((byte)property.Type);
        w.WriteUInt16((ushort)property.Id);
        w.WriteUInt16((ushort)property.Value);
        w.WriteUInt32(property.Extra);
    }

    private static ulong Optional(ulong wireUid) => wireUid == 0 ? 0 : BagItemIds.Stored(wireUid);

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

    private async Task RefuseGrowthAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   maturing compound refused: {reason}");
        await SendAsync(Opcode.S2C_GROWTH_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private async Task RefuseResetAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   mix reset refused: {reason}");
        await SendAsync(Opcode.S2C_RESET_MIX_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
