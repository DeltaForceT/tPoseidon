using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class PetLevelUpController
{
    private readonly MasterEquipStatusUpgrade _master;
    private readonly Func<PetLevelUpTables> _tables;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly TimedGrantAnnouncer _timedGrants;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public PetLevelUpController(
        MasterEquipStatusUpgrade master, Func<PetLevelUpTables> tables, IItemStore items, IItemParamLookup catalog, TimedGrantAnnouncer timedGrants, int bagCapacity, string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _timedGrants = timedGrants ?? throw new ArgumentNullException(nameof(timedGrants));
        _bagCapacity = bagCapacity;
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        await SendAsync(player, Opcode.C2S_TRIGGER_75, Array.Empty<byte>()).ConfigureAwait(false);
        await RequestOpenAsync(player).ConfigureAwait(false);
        return NpcScript.Silent;
    }

    public async Task RequestOpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.PetLevelUp;
        player.UpgradeBusy = false;
        await SendAsync(player, Opcode.S2C_PET_LEVELUP_OPENED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window opened");
    }

    public async Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.PetLevelUp) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, Opcode.S2C_PET_LEVELUP_CLOSED, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "window closed");
    }

    public async Task LevelUpAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!PetLevelUpFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"request of {payload.Length} bytes not read");
            return;
        }
        if (player.Upgrade != UpgradeWindow.PetLevelUp || player.Character is not { } character)
        {
            Log(player, "request outside the pet level-up window");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseAsync(player, PetLevelUpRules.Failed, "a level-up is pending").ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        Item? InBag(ulong wireUid) => wireUid == 0 ? null : bag.FirstOrDefault(item => item.ItemUid == BagItemIds.Stored(wireUid));
        var pet = InBag(request.PetWireUid);
        var catalyst = InBag(request.CatalystWireUid);
        if (catalyst is not null && catalyst.ItemUid == pet?.ItemUid) catalyst = null;
        var petDefinition = pet is null ? null : _catalog.GetById(pet.ItemIndex);
        var code = PetLevelUpRules.Check(petDefinition, catalyst is null ? null : new HeldItem(catalyst.ItemIndex, catalyst.Stack), _tables(), out var row);
        if (code != PetLevelUpRules.Ok || pet is null || catalyst is null || row is null)
        {
            await RefuseAsync(player, code, $"pet 0x{request.PetWireUid:X} catalyst 0x{request.CatalystWireUid:X}").ConfigureAwait(false);
            return;
        }
        if (_catalog.GetById(row.ResultId) is not { } result)
        {
            await RefuseAsync(player, PetLevelUpRules.Failed, $"result pet {row.ResultId} has no item row").ConfigureAwait(false);
            return;
        }
        var rows = new List<EquipUpgradeItem>
        {
            EquipUpgradeOperations.Delete(pet),
            EquipUpgradeOperations.Take(catalyst, (ushort)row.CatalystCount),
            EquipUpgradeOperations.Insert(result.Id, result.ObjectClass, 1, 0, periodMinutes: _timedGrants.PeriodOf(result.Id)),
        };
        player.UpgradeBusy = true;
        EquipStatusUpgradeResult stored;
        try
        {
            stored = await _master.ApplyAsync(new EquipStatusUpgrade(character.CharacterId, 0, _bagCapacity, rows), default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!stored.Succeeded)
        {
            await RefuseAsync(player, PetLevelUpRules.Failed, $"store refused with code {stored.Error}").ConfigureAwait(false);
            return;
        }
        foreach (var removed in stored.Items.Where(item => item.Action == EquipUpgradeAction.Delete)) player.TimedItems.Forget(removed.ItemUid);
        await _timedGrants.AnnounceAsync(player, stored).ConfigureAwait(false);
        await SendAsync(player, Opcode.S2C_PET_LEVELUP_RESULT, PetLevelUpFrames.Result(stored.Items.Select(EquipUpgradeOperations.ToBag).ToList())).ConfigureAwait(false);
        Log(player, $"pet {pet.ItemIndex} -> {row.ResultId} with {row.CatalystCount} x {row.CatalystItemId}");
    }

    private async Task RefuseAsync(InGameSessionState player, ushort code, string reason)
    {
        Log(player, $"refused with code {code}: {reason}");
        await SendAsync(player, Opcode.S2C_PET_LEVELUP_REFUSED, PetLevelUpFrames.Refused(code)).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [pet-levelup] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, Opcode opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync(opcode, payload, randKey, default);
    }
}
