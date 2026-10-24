using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class CashBoxState
{
    public Dictionary<ulong, ChargedItem> Entries { get; } = new();
    public int Busy;
}

internal sealed class CashBoxController
{
    public const int ListedRows = 200;

    private readonly MasterCashBox _master;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public CashBoxController(MasterCashBox master, IItemStore items, IItemParamLookup catalog, string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await ListAsync(player).ConfigureAwait(false);
        return tPoseidon.Core.Game.Quests.NpcScript.Silent;
    }

    public async Task ListAsync(InGameSessionState player)
    {
        if (player.Character is not { } character) return;
        var rows = await _master.ListAsync(character.CharacterId, character.OwnerAccountId, default).ConfigureAwait(false);
        if (rows is null)
        {
            await SendAsync(player, Opcode.S2C_CASH_BOX_LIST_FAILED, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        var listed = rows.Take(ListedRows).ToList();
        lock (player.CashBox.Entries)
        {
            player.CashBox.Entries.Clear();
            foreach (var row in listed) player.CashBox.Entries[row.ItemUid] = row;
        }
        await SendAsync(player, Opcode.S2C_CASH_BOX_LIST, CashBoxFrames.Header(listed.Count)).ConfigureAwait(false);
        foreach (var page in CashBoxFrames.Pages(listed)) await SendAsync(player, Opcode.S2C_CASH_BOX_LIST, page).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} [cashbox] 0x{player.PlayerEntityId:X4} box lists {listed.Count} of {rows.Count} items");
    }

    public async Task TakeAsync(InGameSessionState player, ReadOnlyMemory<byte> body)
    {
        if (player.Character is not { } character || !CashBoxFrames.TryReadTake(body.Span, out var take)) return;
        if (Interlocked.CompareExchange(ref player.CashBox.Busy, 1, 0) != 0) return;
        try
        {
            var uid = BagItemIds.Stored(take.WireUid);
            ChargedItem? entry;
            lock (player.CashBox.Entries) player.CashBox.Entries.TryGetValue(uid, out entry);
            if (entry is null || (uint)entry.ItemIndex != take.ItemId || CashBoxFrames.ListedCount(entry) != take.Count)
            {
                await RefuseAsync(player, $"item {take.ItemId} uid 0x{take.WireUid:X} x{take.Count} is not in the box").ConfigureAwait(false);
                return;
            }
            var common = entry.ItemClass == ChargedItemRules.CommonClass;
            var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
            var joinStack = common && bag.Any(item => item.ItemClass == ChargedItemRules.CommonClass && item.ItemIndex == entry.ItemIndex);
            if (!joinStack && bag.Count >= tPoseidon.Core.Game.Monsters.PickupRules.BagCapacity)
            {
                await RefuseAsync(player, $"item {entry.ItemIndex}: bag full").ConfigureAwait(false);
                return;
            }
            var bagWeight = bag.Sum(item => (_catalog.GetById(item.ItemIndex)?.Weight ?? 0) * item.WeightUnits);
            var addedWeight = (_catalog.GetById(entry.ItemIndex)?.Weight ?? 0) * (common ? CashBoxFrames.ListedCount(entry) : 1);
            if (bagWeight + addedWeight > player.PlayerMaxWt)
            {
                await RefuseAsync(player, $"item {entry.ItemIndex}: weight {bagWeight} + {addedWeight} over {player.PlayerMaxWt}").ConfigureAwait(false);
                return;
            }
            var result = await _master.MoveAsync(new ChargedItemMove(character.OwnerAccountId, uid, character.CharacterId, joinStack), default).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                await RefuseAsync(player, $"item {entry.ItemIndex}: store error {result.Error}").ConfigureAwait(false);
                return;
            }
            var moved = result.Moved!;
            lock (player.CashBox.Entries) player.CashBox.Entries.Remove(uid);
            var bagSide = new BagOperation(
                moved.JoinedStack ? QuestResultFrames.StackChanged : QuestResultFrames.Created,
                moved.ItemClass, (uint)moved.ItemIndex, BagItemIds.OnWire(moved.ItemUid), moved.JoinedStack ? moved.Total : moved.Count);
            var boxSide = new BagOperation(
                QuestResultFrames.Removed, CashBoxFrames.WireClass(entry), (uint)entry.ItemIndex, BagItemIds.OnWire(entry.ItemUid), CashBoxFrames.ListedCount(entry));
            var timed = moved.BreakedAt > Item.NeverBreaks ? moved.BreakedAt : (DateTimeOffset?)null;
            await SendAsync(player, Opcode.S2C_CASH_BOX_TAKEN, CashBoxFrames.Taken(bagSide, boxSide, timed)).ConfigureAwait(false);
            if (timed is { } expiresAt) player.TimedItems.Track(new tPoseidon.Core.Game.Items.TimedItem(moved.ItemUid, moved.ItemIndex, expiresAt));
            Console.WriteLine($"{_logTag} [cashbox] 0x{player.PlayerEntityId:X4} takes item {moved.ItemIndex} x{moved.Count} uid 0x{moved.ItemUid:X}{(timed is { } until ? $" until {until:u}" : string.Empty)}");
        }
        finally
        {
            Volatile.Write(ref player.CashBox.Busy, 0);
        }
    }

    private async Task RefuseAsync(InGameSessionState player, string reason)
    {
        Console.WriteLine($"{_logTag} [cashbox] 0x{player.PlayerEntityId:X4} take refused: {reason}");
        await SendAsync(player, Opcode.S2C_CASH_BOX_TAKE_REFUSED, Array.Empty<byte>()).ConfigureAwait(false);
    }

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
