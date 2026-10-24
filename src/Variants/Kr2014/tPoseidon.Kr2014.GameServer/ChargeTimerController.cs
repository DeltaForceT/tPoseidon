using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.InterServer;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class ChargeTimerController
{
    private readonly MasterItemRecharge _master;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly ItemRechargeCatalog _recharges;
    private readonly string _logTag;
    private readonly Random _rng = new();
    private readonly object _rngGate = new();

    public ChargeTimerController(MasterItemRecharge master, IItemStore items, IItemParamLookup catalog, ItemRechargeCatalog recharges, string logTag)
    {
        _master = master ?? throw new ArgumentNullException(nameof(master));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _recharges = recharges ?? throw new ArgumentNullException(nameof(recharges));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task<int> OpenAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.Upgrade = UpgradeWindow.ChargeTimer;
        player.UpgradeBusy = false;
        await SendAsync(player, ChargeTimerFrames.Opened, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "recharge window opened");
        return NpcScript.Silent;
    }

    public async Task CloseAsync(InGameSessionState player)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (player.Upgrade == UpgradeWindow.ChargeTimer) player.Upgrade = UpgradeWindow.None;
        await SendAsync(player, ChargeTimerFrames.Closed, Array.Empty<byte>()).ConfigureAwait(false);
        Log(player, "recharge window closed");
    }

    public async Task RechargeAsync(InGameSessionState player, ReadOnlyMemory<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (!ChargeTimerFrames.TryReadRequest(payload.Span, out var request))
        {
            Log(player, $"request of {payload.Length} bytes not read");
            return;
        }
        if (player.Upgrade != UpgradeWindow.ChargeTimer || player.Character is not { } character)
        {
            Log(player, "recharge request outside the recharge window");
            return;
        }
        if (player.UpgradeBusy)
        {
            await RefuseAsync(player, RechargeCheck.Busy).ConfigureAwait(false);
            return;
        }
        var now = DateTimeOffset.UtcNow;
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var item = bag.FirstOrDefault(held => held.ItemUid == BagItemIds.Stored(request.ItemWireUid));
        var coupon = bag.FirstOrDefault(held => held.ItemUid == BagItemIds.Stored(request.CouponWireUid));
        var itemDefinition = item is null ? null : _catalog.GetById(item.ItemIndex);
        var couponDefinition = coupon is null ? null : _catalog.GetById(coupon.ItemIndex);
        var check = ItemRechargeRules.Check(request, item, itemDefinition, coupon, couponDefinition,
            item is null ? Array.Empty<RechargeOption>() : _recharges.For(item.ItemIndex), now);
        if (check != RechargeCheck.Ok || item is null || coupon is null)
        {
            await RefuseAsync(player, check).ConfigureAwait(false);
            return;
        }
        player.UpgradeBusy = true;
        ItemRechargeResult result;
        try
        {
            result = await _master.RechargeAsync(new ItemRecharge(character.CharacterId, item.ItemUid, request.LimitUseHours + request.BonusHours, coupon.ItemUid, request.CouponCount), now, default).ConfigureAwait(false);
        }
        finally
        {
            player.UpgradeBusy = false;
        }
        if (!result.Succeeded)
        {
            Log(player, $"store refused the recharge with code {result.Error}");
            await SendAsync(player, ChargeTimerFrames.Refused, Array.Empty<byte>()).ConfigureAwait(false);
            return;
        }
        var couponClass = couponDefinition?.ObjectClass ?? coupon.ItemClass;
        var couponOperation = result.CouponLeft > 0
            ? new BagOperation(QuestResultFrames.StackChanged, couponClass, (uint)coupon.ItemIndex, BagItemIds.OnWire(coupon.ItemUid), (ushort)result.CouponLeft)
            : new BagOperation(QuestResultFrames.Removed, couponClass, (uint)coupon.ItemIndex, BagItemIds.OnWire(coupon.ItemUid), 0);
        if (result.CouponLeft == 0) player.TimedItems.Forget(coupon.ItemUid);
        player.TimedItems.Track(new TimedItem(item.ItemUid, item.ItemIndex, result.BreakedAt));
        await SendAsync(player, ChargeTimerFrames.Recharged, ChargeTimerFrames.Done(BagItemIds.OnWire(item.ItemUid), item.ItemIndex, result.BreakedAt, couponOperation)).ConfigureAwait(false);
        Log(player, $"item {item.ItemIndex} recharged by {request.LimitUseHours} + {request.BonusHours} h until {result.BreakedAt:u} ({request.CouponCount} x coupon {coupon.ItemIndex})");
    }

    private async Task RefuseAsync(InGameSessionState player, RechargeCheck check)
    {
        Log(player, $"recharge refused: {check}");
        await SendAsync(player, ChargeTimerFrames.Refused, Array.Empty<byte>()).ConfigureAwait(false);
    }

    private void Log(InGameSessionState player, string message) =>
        Console.WriteLine($"{_logTag} [recharge] 0x{player.PlayerEntityId:X4} {message}");

    private Task SendAsync(InGameSessionState player, ushort opcode, byte[] payload)
    {
        byte randKey;
        lock (_rngGate) randKey = (byte)_rng.Next(1, 256);
        return player.SendFrameAsync((tPoseidon.Core.Common.Opcode)opcode, payload, randKey, default);
    }
}
