using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Quests;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record ExchangePlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
}

public sealed class ExchangeShopService
{
    private readonly S2SessionContext _ctx;
    private readonly MaterialExchange _exchange;
    private readonly IReadOnlyDictionary<int, ExchangeOffer> _offers;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<int>> _lists;
    private readonly ExchangePlayer _player;
    private readonly Random _rng;
    private readonly string _logTag;

    public ExchangeShopService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        IReadOnlyDictionary<int, ExchangeOffer> offers,
        IReadOnlyDictionary<string, IReadOnlyList<int>> lists,
        ExchangePlayer player,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _exchange = new MaterialExchange(items, catalog);
        _offers = offers ?? throw new ArgumentNullException(nameof(offers));
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsOpen { get; private set; }

    public void Leave() => IsOpen = false;

    public async Task<int> OpenAsync(string listName, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(listName) || !_lists.TryGetValue(listName, out var offerIds))
        {
            Console.WriteLine($"{_logTag} {_ctx.Remote}   exchange: no list '{listName}'");
            return NpcScript.Refused;
        }
        await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
        IsOpen = true;
        await SendAsync(Opcode.S2C_EXCHANGE_LIST, ExchangeFrames.List(offerIds), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   exchange opened: {listName}, {offerIds.Count} offers");
        return NpcScript.Silent;
    }

    public async Task OfferAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !CompoundFrames.TryReadRecipeRequest(payload.Span, out var offerId)) return;
        if (_offers.TryGetValue(offerId, out var offer))
        {
            await SendAsync(Opcode.S2C_EXCHANGE_OFFER, ExchangeFrames.Offer(offer), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_EXCHANGE_OFFER_MISSING, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    public async Task ExchangeAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || !ExchangeFrames.TryReadExchange(payload.Span, out var offerId, out var materials, out var times)) return;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return;
        if (!_offers.TryGetValue(offerId, out var offer))
        {
            await FailAsync($"offer {offerId} is unknown", ct).ConfigureAwait(false);
            return;
        }
        if (materials.Count > CompoundRules.MaxMaterials
            || offer.Fee < 0
            || stats.Money < offer.Fee
            || !_exchange.BagHolds(character.CharacterId, materials)
            || !ExchangeRules.Matches(offer, materials, times))
        {
            await RefuseAsync($"offer {offerId}: money {stats.Money}, fee {offer.Fee}, {materials.Count} materials do not match", ct).ConfigureAwait(false);
            return;
        }
        if (!_exchange.AllKnown(offer.Results) || !_exchange.AllKnown(materials.Select(material => material.ItemId)))
        {
            await FailAsync($"offer {offerId} names an unknown item", ct).ConfigureAwait(false);
            return;
        }
        var grants = offer.Results.Select(result => (result, times)).ToList();
        if (_exchange.Apply(character.CharacterId, materials, grants) is not { } operations)
        {
            await FailAsync("not enough free bag slots", ct).ConfigureAwait(false);
            return;
        }
        if (offer.Fee != 0)
        {
            stats.GrantMoney(-offer.Fee, StatChangeSource.Exchange);
            await SendAsync(Opcode.S2C_MONEY, GroundItemFrames.Money(stats.Money), ct).ConfigureAwait(false);
        }
        await SendAsync(Opcode.S2C_EXCHANGED, ExchangeFrames.Exchanged(operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   exchange {offerId}: fee {offer.Fee}, {operations.Count} bag changes");
    }

    public async Task CloseAsync(CancellationToken ct)
    {
        IsOpen = false;
        await SendAsync(Opcode.S2C_EXCHANGE_CLOSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private async Task RefuseAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   exchange refused: {reason}");
        await SendAsync(Opcode.S2C_COMPOUND_REFUSED, CompoundFrames.Refused(CompoundFrames.RefusedCode), ct).ConfigureAwait(false);
    }

    private async Task FailAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   exchange failed: {reason}");
        await SendAsync(Opcode.S2C_EXCHANGE_FAILED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
