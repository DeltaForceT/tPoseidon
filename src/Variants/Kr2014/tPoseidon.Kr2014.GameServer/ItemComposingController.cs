using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class ItemComposingController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly IItemPropertyStore _properties;
    private readonly ItemManager _itemManager;
    private readonly IReadOnlyList<PetComposingPresent> _presents;
    private readonly IReadOnlyDictionary<int, int> _dummySkins;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public ItemComposingController(
        MasterEquipStatusUpgrade master,
        IItemStore items,
        IItemParamLookup catalog,
        IItemPropertyStore properties,
        ItemManager itemManager,
        IReadOnlyList<PetComposingPresent> presents,
        IReadOnlyDictionary<int, int> dummySkins,
        int bagCapacity,
        string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _presents = presents ?? throw new ArgumentNullException(nameof(presents));
        _dummySkins = dummySkins ?? throw new ArgumentNullException(nameof(dummySkins));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player, byte kind)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = WindowOf(kind);
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_COMPOSING_OPENED, ItemComposingFrames.Opened(kind)).ConfigureAwait(false);
        Log(player, $"{(kind == ItemComposingRules.PetKind ? "pet" : "equipment")} composing window opened");
        return NpcScript.Silent;
    }

    private static UpgradeWindow WindowOf(byte kind) => kind == ItemComposingRules.PetKind ? UpgradeWindow.PetComposing : UpgradeWindow.EquipComposing;

    public async Task CloseAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ItemComposingFrames.TryReadClose(payload.Span, out var value))
        {
            Log(player, $"close request of {payload.Length} bytes not read");
            return;
        }
        if (player.Upgrade is UpgradeWindow.EquipComposing or UpgradeWindow.PetComposing or UpgradeWindow.Decompose) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, Opcode.S2C_COMPOSING_CLOSED, ItemComposingFrames.Closed(value)).ConfigureAwait(false);
        Log(player, $"window closed ({value})");
    }

    public async Task ComposeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ItemComposingFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"request of {payload.Length} bytes not read");
            return;
        }
        if (request.Kind is not (ItemComposingRules.EquipmentKind or ItemComposingRules.PetKind) || player.Upgrade != WindowOf(request.Kind)
            || player.Character is not { } character || player.Stats is not { } stats)
        {
            Log(player, $"request of kind {request.Kind} outside its composing window");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseAsync(player, request.Kind, ItemComposingRules.AlreadyRequesting, "a request is pending").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var baseItem = InBag(request.BaseWireUid);
        var material = InBag(request.MaterialWireUid);
        var support = InBag(request.SupportWireUid);
        if (baseItem is not null && material is not null && baseItem.ItemUid == material.ItemUid)
        {
            await RefuseAsync(player, request.Kind, ItemComposingRules.NotInBag, "base and material are the same item").ConfigureAwait(false);
            return;
        }
        var baseProperties = baseItem is null ? Array.Empty<ItemProperty>() : _properties.ForItem(baseItem.ItemUid);
        var materialProperties = material is null ? Array.Empty<ItemProperty>() : _properties.ForItem(material.ItemUid);
        var baseDefinition = baseItem is null ? null : _catalog.GetById(baseItem.ItemIndex);
        var materialDefinition = material is null ? null : _catalog.GetById(material.ItemIndex);
        var candidate = new ComposingCandidate(
            baseDefinition,
            baseProperties,
            materialDefinition,
            materialProperties,
            request.SupportWireUid != 0,
            support is null ? null : _catalog.GetById(support.ItemIndex),
            request.Kind);
        var code = ItemComposingRules.Check(candidate);
        if (code != ItemComposingRules.Ok || baseItem is null || material is null || baseDefinition is null)
        {
            await RefuseAsync(player, request.Kind, code, $"base 0x{request.BaseWireUid:X} material 0x{request.MaterialWireUid:X} support 0x{request.SupportWireUid:X}").ConfigureAwait(false);
            return;
        }
        var usesCatalyst = ItemComposingRules.NeedsCatalyst(baseDefinition, baseProperties) ? support : null;
        var look = ItemComposingRules.Look(baseDefinition, baseProperties, material.ItemIndex, materialProperties);
        var rows = new List<EquipUpgradeItem>(2) { EquipUpgradeOperations.Delete(material) };
        if (usesCatalyst is not null) rows.Add(EquipUpgradeOperations.Take(usesCatalyst, 1));
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult result;
        try
        {
            result = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded)
        {
            Log(player, $"store refused with code {result.Error}; nothing composed");
            return;
        }
        _properties.Replace(baseItem.ItemUid, ItemComposingRules.Apply(baseProperties, look));
        stats.GrantMoney(result.RemainMoney - stats.Money, tPoseidon.Core.Game.Stats.StatChangeSource.EquipUpgrade);
        player.TimedItems.Forget(material.ItemUid);
        var materialOperation = EquipUpgradeOperations.ToBag(result.Items.First(row => row.ItemUid == material.ItemUid));
        BagOperation? supportOperation = usesCatalyst is null ? null : EquipUpgradeOperations.ToBag(result.Items.First(row => row.ItemUid == usesCatalyst.ItemUid));
        await SendAsync(player, Opcode.S2C_COMPOSING_RESULT, ItemComposingFrames.Result(
            request.Kind,
            materialOperation,
            supportOperation,
            BagItemIds.OnWire(baseItem.ItemUid),
            ItemComposingRules.AppearanceProperty(look),
            ItemComposingRules.OptionsProperty(look),
            result.RemainMoney)).ConfigureAwait(false);
        Log(player, $"item {baseItem.ItemIndex} takes the look of item {look.AppearanceItemId} (material {material.ItemIndex} consumed, options 0x{look.Options:X}, {(look.Inserts ? "new" : "replaced")} look, catalyst {(usesCatalyst is null ? "none" : usesCatalyst.ItemIndex.ToString(System.Globalization.CultureInfo.InvariantCulture))})");
        if (request.Kind == ItemComposingRules.PetKind && materialDefinition is not null
            && ItemComposingRules.PetPresent(_presents, baseDefinition.MinLevel, (materialDefinition.Options & ItemComposingRules.CashPetOption) != 0) is int present)
        {
            await GivePresentAsync(player, character.CharacterId, present).ConfigureAwait(false);
        }
    }

    public async Task<int> OpenDecomposeAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.Decompose;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await SendAsync(player, (Opcode)ItemComposingFrames.DecomposeOpened, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "decompose window opened");
        return NpcScript.Silent;
    }

    public async Task DecomposeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ItemComposingFrames.TryReadDecompose(payload.Span, out var request))
        {
            Log(player, $"decompose request of {payload.Length} bytes not read");
            return;
        }
        if (player.Upgrade != UpgradeWindow.Decompose || player.Character is not { } character)
        {
            Log(player, "decompose request outside the decompose window");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseDecomposeAsync(player, ItemComposingRules.AlreadyRequesting, "a request is pending").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var item = bag.FirstOrDefault(held => held.ItemUid == BagItemIds.Stored(request.ItemWireUid));
        var catalyst = bag.FirstOrDefault(held => held.ItemUid == BagItemIds.Stored(request.CatalystWireUid));
        if (item is null || catalyst is null || item.ItemUid == catalyst.ItemUid || _catalog.GetById(item.ItemIndex) is not { } definition)
        {
            await RefuseDecomposeAsync(player, DecomposeRules.NotInBag, $"item 0x{request.ItemWireUid:X} or catalyst 0x{request.CatalystWireUid:X} not in the bag").ConfigureAwait(false);
            return;
        }
        var properties = _properties.ForItem(item.ItemUid);
        var code = DecomposeRules.Check(properties, _catalog.GetById(catalyst.ItemIndex));
        if (code != ItemComposingRules.Ok)
        {
            await RefuseDecomposeAsync(player, code, $"item {item.ItemIndex} with {properties.Count} properties, catalyst {catalyst.ItemIndex}").ConfigureAwait(false);
            return;
        }
        var look = properties.First(property => property.Type == ItemComposingRules.AppearanceType);
        var lookItemId = unchecked((int)look.Extra);
        var lookDefinition = _catalog.GetById(lookItemId);
        int skinItemId;
        byte skinSlot;
        IReadOnlyList<ItemProperty> skinProperties;
        if (lookDefinition is not null && (lookDefinition.Options & DecomposeRules.OriginalLookOption) != 0)
        {
            (skinItemId, skinSlot, skinProperties) = (lookItemId, DecomposeRules.OriginalMixSlot, Array.Empty<ItemProperty>());
        }
        else if (DecomposeRules.DummyRow(definition.Type, definition.SubType) is int row && _dummySkins.TryGetValue(row, out var dummy))
        {
            (skinItemId, skinSlot, skinProperties) = (dummy, DecomposeRules.DummyMixSlot, properties.Where(DecomposeRules.IsLookProperty).ToArray());
        }
        else
        {
            await RefuseDecomposeAsync(player, DecomposeRules.NotDecomposable, $"no dummy skin for item kind {definition.Type}/{definition.SubType}").ConfigureAwait(false);
            return;
        }
        if (bag.Count >= _bagCapacity)
        {
            await RefuseDecomposeAsync(player, DecomposeRules.BagFull, "bag full").ConfigureAwait(false);
            return;
        }
        var skinClass = _catalog.GetById(skinItemId)?.ObjectClass ?? (byte)ItemParamEntry.RareClass;
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult result;
        try
        {
            result = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity,
                [EquipUpgradeOperations.Take(catalyst, 1), EquipUpgradeOperations.Insert(skinItemId, skinClass, 1, skinSlot)]), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded || result.Items.FirstOrDefault(row => row.Action == EquipUpgradeAction.Insert) is not { } skinRow)
        {
            Log(player, $"store refused the decompose with code {result.Error}");
            await RefuseDecomposeAsync(player, DecomposeRules.NotDecomposable, "store refused").ConfigureAwait(false);
            return;
        }
        var kept = properties.Where(property => !DecomposeRules.IsLookProperty(property)).ToArray();
        _properties.Replace(item.ItemUid, kept);
        if (skinProperties.Count > 0) _properties.Replace(skinRow.ItemUid, skinProperties);
        var catalystOperation = EquipUpgradeOperations.ToBag(result.Items.First(row => row.ItemUid == catalyst.ItemUid));
        await SendAsync(player, (Opcode)ItemComposingFrames.Decomposed, ItemComposingFrames.DecomposeResult(
            BagItemIds.OnWire(item.ItemUid), catalystOperation, EquipUpgradeOperations.ToBag(skinRow), kept)).ConfigureAwait(false);
        Log(player, $"item {item.ItemIndex} decomposed: look {lookItemId} back as item {skinItemId}, {kept.Length} properties kept, catalyst {catalyst.ItemIndex}");
    }

    private async Task RefuseDecomposeAsync(InGameSessionState player, byte code, string reason)
    {
        Log(player, $"decompose refused with code {code}: {reason}");
        await SendAsync(player, (Opcode)ItemComposingFrames.DecomposeRefused, ItemComposingFrames.DecomposeCode(code)).ConfigureAwait(false);
    }

    private async Task GivePresentAsync(InGameSessionState player, ulong characterId, int itemId)
    {
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        if (bag.Count >= _bagCapacity)
        {
            Log(player, $"pet composing present {itemId}: bag full, not given");
            return;
        }
        var itemClass = _catalog.GetById(itemId)?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var minted = _itemManager.GrantRewardItem(characterId, itemId, 1, itemClassOverride: itemClass);
        await SendAsync(player, Opcode.S2C_ITEM_OPERATION, QuestResultFrames.ItemOperation(new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId,
            BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot))).ConfigureAwait(false);
        Log(player, $"pet composing present {itemId} given");
    }

    private async Task RefuseAsync(InGameSessionState player, byte kind, byte code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_COMPOSING_REFUSED, ItemComposingFrames.Refused(kind, code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [composing] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
