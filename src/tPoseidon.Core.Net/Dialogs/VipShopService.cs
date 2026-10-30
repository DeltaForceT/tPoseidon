using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record VipShopPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<int> MaxWeight { get; init; }
    public Func<IReadOnlyList<TimedItem>, CancellationToken, Task>? TimedItemsGranted { get; init; }
}

public sealed class VipShopService
{
    public const ushort UnknownOffer = 1;
    public const ushort NoEggs = 5;
    public const ushort TooFewEggs = 6;
    public const ushort TooHeavy = 7;
    public const ushort Failed = 8;

    private readonly S2SessionContext _ctx;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly MaterialExchange _exchange;
    private readonly IReadOnlyDictionary<int, VipShopOffer> _offers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<int>> _lists;
    private readonly VipShopPlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;

    public VipShopService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        IReadOnlyDictionary<int, VipShopOffer> offers,
        IReadOnlyDictionary<string, IReadOnlyList<int>> lists,
        VipShopPlayer player,
        Random rng,
        string logTag,
        ItemManager? periods = null)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _exchange = new MaterialExchange(items, catalog, periods);
        _offers = offers ?? throw new ArgumentNullException(nameof(offers));
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsOpen { get; private set; }

    public void Leave() => IsOpen = false;

    public static byte[] List(IReadOnlyList<int> offerIds)
    {
        ArgumentNullException.ThrowIfNull(offerIds);
        var shown = offerIds.Take(VipShopOffer.MaxListed).ToList();
        var w = new PacketWriter(2 + shown.Count * 4);
        w.WriteUInt16((ushort)shown.Count);
        foreach (var id in shown) w.WriteUInt32((uint)id);
        return w.ToArray();
    }

    public async Task<int> OpenAsync(string listName, CancellationToken ct)
    {
        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        IsOpen = true;
        if (string.IsNullOrEmpty(listName) || !_lists.TryGetValue(listName, out var offerIds))
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   egg shop: no list '{listName}'");
            return NpcScript.Refused;
        }
        await SendAsync(Opcode.S2C_VIP_SHOP_LIST, List(offerIds), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   egg shop opened: {listName}, {Math.Min(offerIds.Count, VipShopOffer.MaxListed)} of {offerIds.Count} offers");
        return NpcScript.Silent;
    }

    public async Task BuyAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < sizeof(uint) || _player.Character() is not { } character) return;
        var offerId = (int)BinaryPrimitives.ReadUInt32LittleEndian(payload.Span);
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        if (bag.FirstOrDefault(item => item.ItemIndex == VipShopOffer.EggItemId) is not { } eggs)
        {
            await RefuseAsync(NoEggs, $"offer {offerId}: no eggs in the bag", ct).ConfigureAwait(false);
            return;
        }
        if (!_offers.TryGetValue(offerId, out var offer))
        {
            await RefuseAsync(UnknownOffer, $"offer {offerId} is unknown", ct).ConfigureAwait(false);
            return;
        }
        if (offer.EggCost <= 0 || eggs.Stack < offer.EggCost)
        {
            await RefuseAsync(TooFewEggs, $"offer {offerId} costs {offer.EggCost} eggs, the stack holds {eggs.Stack}", ct).ConfigureAwait(false);
            return;
        }
        var bagWeight = bag.Sum(item => (_catalog.GetById(item.ItemIndex)?.Weight ?? 0) * item.WeightUnits);
        var grantWeight = (_catalog.GetById(offer.ItemId)?.Weight ?? 0) * offer.ItemCount;
        if (bagWeight + grantWeight > _player.MaxWeight())
        {
            await RefuseAsync(TooHeavy, $"offer {offerId}: weight {bagWeight} + {grantWeight} over {_player.MaxWeight()}", ct).ConfigureAwait(false);
            return;
        }
        if (offer.ItemId == 0 || !_exchange.AllKnown(new[] { offer.ItemId })
            || _exchange.Apply(
                character.CharacterId,
                new[] { new CompoundMaterial(BagItemIds.OnWire(eggs.ItemUid), VipShopOffer.EggItemId, offer.EggCost) },
                new[] { (offer.ItemId, offer.ItemCount) }) is not { } operations)
        {
            await RefuseAsync(Failed, $"offer {offerId}: item {offer.ItemId} cannot be given (unknown item or no free bag slot)", ct).ConfigureAwait(false);
            return;
        }
        if (_exchange.TakeTimedGrants() is { Count: > 0 } timedGrants && _player.TimedItemsGranted is { } announce) await announce(timedGrants, ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_VIP_SHOP_BOUGHT, ExchangeFrames.Exchanged(operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   egg shop: offer {offerId} gives item {offer.ItemId} x{offer.ItemCount} for {offer.EggCost} eggs");
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        IsOpen = false;
        await SendAsync(Opcode.S2C_VIP_SHOP_CLOSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private async Task RefuseAsync(ushort code, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   egg shop refused ({code}): {reason}");
        var w = new PacketWriter(2);
        w.WriteUInt16(code);
        await SendAsync(Opcode.S2C_VIP_SHOP_REFUSED, w.ToArray(), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
