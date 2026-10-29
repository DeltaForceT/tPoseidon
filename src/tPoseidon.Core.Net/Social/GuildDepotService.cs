using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Social;

public sealed record GuildDepotPlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<int> MaxWeight { get; init; }
    public required Func<ulong, int, IReadOnlyList<ItemProperty>> Properties { get; init; }
}

public sealed class GuildDepotService
{
    private readonly S2SessionContext _ctx;
    private readonly IGuildDepotStore _depot;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly GuildDepotPlayer _player;
    private readonly Encoding _text;
    private readonly Random _rng;
    private readonly string _logTag;

    public GuildDepotService(
        S2SessionContext ctx,
        IGuildDepotStore depot,
        IItemStore items,
        IItemParamLookup catalog,
        GuildDepotPlayer player,
        Encoding text,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _depot = depot ?? throw new ArgumentNullException(nameof(depot));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _text = text ?? throw new ArgumentNullException(nameof(text));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public bool IsOpen { get; private set; }

    public void Leave() => IsOpen = false;

    public Task OpenTabAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!GuildDepotFrames.TryReadTab(payload.Span, out var tab)) return Task.CompletedTask;
        if (Gate(tab) is not { } gate) return RefuseAsync(GuildDepotErrors.Unknown, "no character", ct);
        if (gate.Error != GuildDepotErrors.None) return RefuseAsync(gate.Error, $"tab {tab} refused", ct);
        IsOpen = true;
        var listings = _depot.Contents(gate.Access!.GuildCode, tab)
            .Select(Normalize)
            .Select(item => new GuildDepotListing(item, PropertiesOf(item)))
            .ToList();
        Console.WriteLine($"{_logTag} {_ctx.Remote}   guild depot: tab {tab} opened, {listings.Count} entries");
        return SendAsync(Opcode.S2C_GUILD_DEPOT_TAB, GuildDepotFrames.TabContents(tab, listings), ct);
    }

    public Task HistoryAsync(CancellationToken ct)
    {
        if (!IsOpen) return RefuseAsync(GuildDepotErrors.Unusable, "history asked while the window is closed", ct);
        if (Gate(null) is not { } gate) return RefuseAsync(GuildDepotErrors.Unknown, "no character", ct);
        if (gate.Error != GuildDepotErrors.None) return RefuseAsync(gate.Error, "history refused", ct);
        return SendAsync(Opcode.S2C_GUILD_DEPOT_HISTORY, GuildDepotFrames.History(_depot.History(gate.Access!.GuildCode), _text), ct);
    }

    public async Task DepositAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!GuildDepotFrames.TryReadItemRequest(payload.Span, out var wireUid, out var count, out var tab)) return;
        if (!IsOpen)
        {
            await RefuseAsync(GuildDepotErrors.Unusable, "deposit while the window is closed", ct).ConfigureAwait(false);
            return;
        }
        if (Gate(tab) is not { } gate || _player.Character() is not { } character)
        {
            await RefuseAsync(GuildDepotErrors.Unknown, "no character", ct).ConfigureAwait(false);
            return;
        }
        if (gate.Error != GuildDepotErrors.None)
        {
            await RefuseAsync(gate.Error, $"deposit into tab {tab} refused", ct).ConfigureAwait(false);
            return;
        }
        var uid = BagItemIds.Stored(wireUid);
        var held = _items.FindByUid(uid);
        if (held is null || held.CharacterId != character.CharacterId || held.SlotKind != SlotKind.Inventory)
        {
            await RefuseAsync(GuildDepotErrors.ItemMissing, $"0x{uid:X} is not in the bag", ct).ConfigureAwait(false);
            return;
        }
        if (_catalog.GetById(held.ItemIndex) is not { } definition)
        {
            await RefuseAsync(GuildDepotErrors.BadItem, $"item {held.ItemIndex} is unknown", ct).ConfigureAwait(false);
            return;
        }
        if ((definition.Options & GuildDepotRules.NotStorableOption) != 0)
        {
            await RefuseAsync(GuildDepotErrors.NotStorable, $"item {held.ItemIndex} cannot be stored", ct).ConfigureAwait(false);
            return;
        }

        var outcome = _depot.Deposit(new GuildDepotDeposit(
            gate.Access!.GuildCode, character.CharacterId, character.Name, uid, (ushort)Math.Min(count, ushort.MaxValue), tab, definition.IsStackable));
        if (outcome.Error != GuildDepotErrors.None)
        {
            await RefuseAsync(outcome.Error, $"deposit of {count} of 0x{uid:X} refused", ct).ConfigureAwait(false);
            return;
        }
        var moved = MovedProperties(outcome.Depot!.Item, definition);
        await SendAsync(Opcode.S2C_GUILD_DEPOT_DEPOSITED, GuildDepotFrames.Transfer(tab, Normalize(outcome.Depot), Normalize(outcome.Bag!), moved, outcome.History!, _text), ct)
            .ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   guild depot: deposited {held.ItemIndex} x{count} into tab {tab}");
    }

    public async Task WithdrawAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!GuildDepotFrames.TryReadItemRequest(payload.Span, out var wireUid, out var count, out var tab)) return;
        if (!IsOpen)
        {
            await RefuseAsync(GuildDepotErrors.Unusable, "withdrawal while the window is closed", ct).ConfigureAwait(false);
            return;
        }
        if (Gate(tab) is not { } gate || _player.Character() is not { } character)
        {
            await RefuseAsync(GuildDepotErrors.Unknown, "no character", ct).ConfigureAwait(false);
            return;
        }
        if (gate.Error != GuildDepotErrors.None)
        {
            await RefuseAsync(gate.Error, $"withdrawal from tab {tab} refused", ct).ConfigureAwait(false);
            return;
        }
        var uid = BagItemIds.Stored(wireUid);
        var stored = _depot.Contents(gate.Access!.GuildCode, tab).FirstOrDefault(entry => entry.ItemUid == uid);
        if (stored is null)
        {
            await RefuseAsync(GuildDepotErrors.ItemMissing, $"tab {tab} does not hold 0x{uid:X}", ct).ConfigureAwait(false);
            return;
        }
        if (_catalog.GetById(stored.ItemIndex) is not { } definition)
        {
            await RefuseAsync(GuildDepotErrors.BadItem, $"item {stored.ItemIndex} is unknown", ct).ConfigureAwait(false);
            return;
        }

        var outcome = _depot.Withdraw(new GuildDepotWithdrawal(
            gate.Access.GuildCode, character.CharacterId, character.Name, uid, (ushort)Math.Min(count, ushort.MaxValue), tab,
            definition.IsStackable, definition.Weight, WeightRoom(character.CharacterId, definition.Weight)));
        if (outcome.Error != GuildDepotErrors.None)
        {
            await RefuseAsync(outcome.Error, $"withdrawal of {count} of 0x{uid:X} refused", ct).ConfigureAwait(false);
            return;
        }
        var moved = MovedProperties(outcome.Bag!.Item, definition);
        await SendAsync(Opcode.S2C_GUILD_DEPOT_WITHDRAWN, GuildDepotFrames.Transfer(tab, Normalize(outcome.Depot!), Normalize(outcome.Bag), moved, outcome.History!, _text), ct)
            .ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   guild depot: withdrew {stored.ItemIndex} x{count} from tab {tab}");
    }

    private (GuildDepotAccess? Access, int Error)? Gate(byte? tab)
    {
        if (_player.Character() is not { } character) return null;
        var access = _depot.AccessOf(character.CharacterId);
        return (access, GuildDepotRules.Admit(access, tab));
    }

    private int WeightRoom(ulong characterId, int unitWeight)
    {
        if (unitWeight == 0) return int.MaxValue;
        var carried = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory)
            .Sum(item => (long)(_catalog.GetById(item.ItemIndex)?.Weight ?? 0) * item.WeightUnits);
        return (int)Math.Clamp(_player.MaxWeight() - carried, int.MinValue, int.MaxValue);
    }

    private GuildDepotItem Normalize(GuildDepotItem item) =>
        item with { ItemClass = _catalog.GetById(item.ItemIndex)?.ObjectClass ?? item.ItemClass };

    private GuildDepotSide Normalize(GuildDepotSide side) => side with { Item = Normalize(side.Item) };

    private IReadOnlyList<ItemProperty> PropertiesOf(GuildDepotItem item) =>
        item.ItemClass == ItemParamEntry.RareClass ? _player.Properties(item.ItemUid, item.ItemIndex) : Array.Empty<ItemProperty>();

    private IReadOnlyList<ItemProperty> MovedProperties(GuildDepotItem item, ItemParamEntry definition) =>
        definition.ObjectClass == ItemParamEntry.RareClass ? _player.Properties(item.ItemUid, item.ItemIndex) : Array.Empty<ItemProperty>();

    private Task RefuseAsync(int code, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   guild depot: {reason} (code {code})");
        return SendAsync(Opcode.S2C_GUILD_DEPOT_ERROR, GuildDepotFrames.Error(code), ct);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }
}
