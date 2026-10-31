using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Shop;
using tPoseidon.Core.Game.Skills;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Dialogs;

public sealed record SkillShopCatalogs
{
    public required SkillShopManager SkillShops { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<ShopGoodsEntry>> GoodsByTable { get; init; }
    public required IReadOnlyDictionary<int, float> PriceRateByMap { get; init; }
    public required IReadOnlyDictionary<int, SkillMasterEntry> MasterItems { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyList<int>> MasterLists { get; init; }
    public required SkillBook Skills { get; init; }
    public required IReadOnlyDictionary<int, SkillCardEntry> Cards { get; init; }
    public required IItemParamLookup Items { get; init; }
}

public sealed record SkillShopPlayer
{
    public required ShopPlayer Shopper { get; init; }
    public required Func<ushort> EntityId { get; init; }
    public required Func<ushort> TypeIndex { get; init; }
    public required Func<IReadOnlyDictionary<int, int>> Skills { get; init; }
    public required Action<int, int> SetSkillLevel { get; init; }
    public required Func<ushort, Opcode, byte[], Task> SendToMap { get; init; }
    public Func<int>? WornCount { get; init; }
    public Func<int>? FreeBagSlots { get; init; }
    public Func<byte, ushort, byte, CancellationToken, Task<bool>>? ChangeJob { get; init; }
    public IJobQuests? JobQuests { get; init; }
}

public sealed class SkillShopService
{
    public const byte ExitFunction = 1;
    public const byte BuyListFunction = 0x24;
    public const byte SellListFunction = 0x25;
    public const byte ShopExitFunction = 0x26;
    public const byte IdleFunction = 0x27;
    public const byte MasterViewFunction = 0x3B;
    public const byte MasterViewCloseFunction = 0x3C;
    public const byte JobChangeViewFunction = 0x40;
    public const byte SecondJobQuestCheck = 61;
    public const byte SecondJobQuestAccept = 62;
    public const byte SecondJobQuestComplete = 63;
    public const byte ThirdJobQuestCheck = 111;
    public const byte ThirdJobQuestAccept = 112;
    public const byte ThirdJobQuestComplete = 113;
    public const int MasterableLevel = 10;

    private readonly S2SessionContext _ctx;
    private readonly SkillShopCatalogs _catalogs;
    private readonly SkillShopPlayer _player;
    private readonly IItemStore _items;
    private readonly ShopTrade _trade;
    private readonly Encoding _textEncoding;
    private readonly Random _rng;
    private readonly string _logTag;

    public SkillShopService(
        S2SessionContext ctx,
        SkillShopCatalogs catalogs,
        SkillShopPlayer player,
        IItemStore items,
        ItemManager itemManager,
        Encoding textEncoding,
        Random rng,
        string logTag)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _catalogs = catalogs ?? throw new ArgumentNullException(nameof(catalogs));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _textEncoding = textEncoding ?? throw new ArgumentNullException(nameof(textEncoding));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _trade = new ShopTrade(ctx, catalogs.Items, player.Shopper, items, itemManager, rng, logTag);
    }

    public bool IsOpen { get; private set; }

    public void Leave() => IsOpen = false;

    public async Task OpenAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (payload.Length < SkillShopFrames.OpenRequestSize) return;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        if (FindShop(npcEntityId) is not { } shop)
        {
            await SendAsync(Opcode.S2C_SKILL_SHOP_NOT_FOUND, Array.Empty<byte>(), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_ctx.Remote}   skill shop 0x{npcEntityId:X4} not on this map");
            return;
        }
        IsOpen = true;
        var pageCount = shop.Pages.Count(page => !string.IsNullOrWhiteSpace(page.Text));
        var firstPage = shop.Pages.Count > 0 ? shop.Pages[0].Text : string.Empty;
        await SendAsync(Opcode.S2C_SKILL_SHOP_OPENED,
            SkillShopFrames.Opened(npcEntityId, (ushort)shop.NpcType, pageCount, firstPage, _textEncoding), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   skill shop 0x{npcEntityId:X4} (NPC {shop.NpcType}, cards {shop.ItemTable}) opened");
    }

    public async Task TalkAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < SkillShopFrames.TalkRequestSize) return;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        var index = payload.Span[2];
        if (FindShop(npcEntityId) is not { } shop)
        {
            await RefuseForwardedAsync(ct).ConfigureAwait(false);
            return;
        }
        if (index < shop.Pages.Count && !string.IsNullOrWhiteSpace(shop.Pages[index].Text))
        {
            var (kind, text) = shop.Pages[index];
            await SendAsync(Opcode.S2C_SKILL_SHOP_PAGE,
                SkillShopFrames.Page(npcEntityId, text, (ushort)kind, (ushort)shop.JobQuestId, index, _textEncoding), ct).ConfigureAwait(false);
            return;
        }
        Console.WriteLine($"{_logTag} {_ctx.Remote}   skill shop 0x{npcEntityId:X4}: no page {index}");
        await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, SkillShopFrames.NpcOnly(npcEntityId), ct).ConfigureAwait(false);
    }

    public async Task ScriptAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < SkillShopFrames.ScriptHeaderSize) return;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        var function = payload.Span[2];
        if (FindShop(npcEntityId) is not { } shop)
        {
            await RefuseForwardedAsync(ct).ConfigureAwait(false);
            return;
        }
        switch (function)
        {
            case ExitFunction:
            case ShopExitFunction:
                IsOpen = false;
                await SendAsync(Opcode.SKILL_SHOP_CLOSE, Array.Empty<byte>(), ct).ConfigureAwait(false);
                return;
            case BuyListFunction:
                await SendAsync(Opcode.S2C_SKILL_CARD_PRICES, ShopFrames.BuyPrices(_trade.BuyPrices(OfferOf(shop))), ct).ConfigureAwait(false);
                return;
            case SellListFunction:
                var (shopRate, rates) = ShopTrade.SellRates(OfferOf(shop));
                await SendAsync(Opcode.S2C_SKILL_CARD_RATES, ShopFrames.SellRates(shopRate, rates), ct).ConfigureAwait(false);
                return;
            case MasterViewFunction:
                await SendAsync(Opcode.S2C_SKILL_MASTER_LIST, SkillShopFrames.MasterList(MasterableSkills(shop)), ct).ConfigureAwait(false);
                return;
            case MasterViewCloseFunction:
                await SendAsync(Opcode.S2C_SKILL_MASTER_CLOSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
                return;
            case JobChangeViewFunction:
                await SendAsync(Opcode.S2C_JOB_CHANGE_VIEW,
                    SkillShopFrames.JobChangeView((ushort)shop.ChangeType, (ushort)shop.ChangeJob), ct).ConfigureAwait(false);
                return;
            case IdleFunction:
                return;
            case SecondJobQuestCheck:
            case SecondJobQuestAccept:
            case SecondJobQuestComplete:
            case ThirdJobQuestCheck:
            case ThirdJobQuestAccept:
            case ThirdJobQuestComplete:
                await RunJobQuestAsync(npcEntityId, shop, function, ReadScriptArgs(payload.Span), ct).ConfigureAwait(false);
                return;
            default:
                Console.WriteLine($"{_logTag} {_ctx.Remote}   skill shop 0x{npcEntityId:X4}: function {function} is not implemented; refused");
                await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
                return;
        }
    }

    public async Task ParamScriptAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < SkillShopFrames.ScriptHeaderSize) return;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        var function = payload.Span[2];
        if (FindShop(npcEntityId) is not { } shop)
        {
            await RefuseForwardedAsync(ct).ConfigureAwait(false);
            return;
        }
        switch (function)
        {
            case ExitFunction:
                IsOpen = false;
                await SendAsync(Opcode.C2S_TRIGGER_75, Array.Empty<byte>(), ct).ConfigureAwait(false);
                return;
            case IdleFunction:
                return;
            case tPoseidon.Core.Game.Quests.SkillQuestRules.CheckFunction:
                await RunSkillQuestCheckAsync(npcEntityId, shop, payload, ct).ConfigureAwait(false);
                return;
            default:
                Console.WriteLine($"{_logTag} {_ctx.Remote}   skill shop 0x{npcEntityId:X4}: parameter function {function} is not implemented; refused");
                await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
                return;
        }
    }

    public async Task SendBuyPricesAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < SkillShopFrames.OpenRequestSize) return;
        if (FindShop(BinaryPrimitives.ReadUInt16LittleEndian(payload.Span)) is not { } shop)
        {
            await SendAsync(Opcode.S2C_SKILL_CARD_PRICES_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_SKILL_CARD_PRICES, ShopFrames.BuyPrices(_trade.BuyPrices(OfferOf(shop))), ct).ConfigureAwait(false);
    }

    public async Task SendSellRatesAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < SkillShopFrames.OpenRequestSize) return;
        if (FindShop(BinaryPrimitives.ReadUInt16LittleEndian(payload.Span)) is not { } shop)
        {
            await SendAsync(Opcode.S2C_SKILL_CARD_RATES_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var (shopRate, rates) = ShopTrade.SellRates(OfferOf(shop));
        await SendAsync(Opcode.S2C_SKILL_CARD_RATES, ShopFrames.SellRates(shopRate, rates), ct).ConfigureAwait(false);
    }

    public async Task BuyAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen) return;
        var refusal = await _trade.TryBuyAsync(payload.ToArray(), OfferById, ct).ConfigureAwait(false);
        if (refusal is Opcode refused) await SendAsync(refused, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    public async Task SellAsync(byte[] body, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (!IsOpen) return;
        var refusal = await _trade.TrySellAsync(body, OfferById, ct).ConfigureAwait(false);
        if (refusal is (Opcode opcode, byte[] refusalPayload)) await SendAsync(opcode, refusalPayload, ct).ConfigureAwait(false);
    }

    public async Task MasterAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (payload.Length < SkillShopFrames.MasterRequestSize) return;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        var skillId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span[2..]);
        if (FindShop(npcEntityId) is not { } shop
            || !_player.Skills().TryGetValue(skillId, out var heldLevel)
            || _player.Shopper.Character() is not { } character
            || _player.Shopper.Stats() is not { } stats)
        {
            await RefuseMasterAsync(skillId, "no such skill shop or skill", ct).ConfigureAwait(false);
            return;
        }

        var row = _catalogs.Skills.Find(skillId)?.Row;
        var refusal = SkillLevelling.Check(row, SkillLevelling.HasCard(row, _catalogs.Cards), heldLevel, SkillLevelling.MasterLevel, stats.TmPoint);
        if (refusal == SkillLevelRefusal.NotEnoughTmForMaster)
        {
            await SendAsync(Opcode.S2C_SKILL_MASTER_LIST, SkillShopFrames.MasterList(MasterableSkills(shop)), ct).ConfigureAwait(false);
            await SendAsync(Opcode.S2C_SKILL_MASTER_TM_SHORT, Array.Empty<byte>(), ct).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} {_ctx.Remote}   mastering skill {skillId} refused: {stats.TmPoint} TM points of {row?.MasterTmPoint}");
            return;
        }
        if (refusal is { } refused)
        {
            if (refused == SkillLevelRefusal.AboveMaxLevel)
            {
                await SendAsync(Opcode.S2C_SKILL_NOTICE,
                    SkillFrames.Notice(SkillLevelling.RegistrationNotice, skillId, SkillLevelling.AboveMaxLevelNotice), ct).ConfigureAwait(false);
            }
            await RefuseMasterAsync(skillId, refused.ToString(), ct).ConfigureAwait(false);
            return;
        }
        if (!_catalogs.MasterItems.TryGetValue(skillId, out var master))
        {
            await RefuseMasterAsync(skillId, "no SkillMasterInfo row", ct).ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory);
        var first = bag.FirstOrDefault(item => item.ItemIndex == master.ItemId);
        if (first is null || bag.Where(item => item.ItemIndex == master.ItemId).Sum(item => item.Stack) < master.ItemCount)
        {
            await RefuseMasterAsync(skillId, $"needs {master.ItemCount} of item {master.ItemId}", ct).ConfigureAwait(false);
            return;
        }

        var itemClass = _catalogs.Items.GetById(master.ItemId)?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        BagOperation taken;
        if (first.Stack > master.ItemCount)
        {
            var left = _items.AdjustStack(first.ItemUid, -master.ItemCount);
            taken = new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)master.ItemId, BagItemIds.OnWire(first.ItemUid), left);
        }
        else
        {
            _items.Delete(first.ItemUid);
            taken = new BagOperation(QuestResultFrames.Removed, itemClass, (uint)master.ItemId, BagItemIds.OnWire(first.ItemUid), 0);
        }

        var masterPoints = row!.MasterTmPoint;
        stats.SpendTmPoints(masterPoints);
        _player.SetSkillLevel(skillId, SkillLevelling.MasterLevel);
        await SendAsync(Opcode.S2C_SKILL_LEVELLED,
            SkillFrames.Record(skillId, SkillLevelling.MasterLevel, (ushort)masterPoints), ct).ConfigureAwait(false);
        await _player.SendToMap(_player.Shopper.MapId(), Opcode.S2C_SKILL_LEVEL_SEEN,
            SkillFrames.LevelSeen(_player.EntityId(), _player.TypeIndex(), SkillLevelling.MasterLevel)).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_SKILL_MASTER_ITEMS, QuestResultFrames.ItemOperation(taken), ct).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_SKILL_MASTER_LIST, SkillShopFrames.MasterList(MasterableSkills(shop)), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   skill {skillId} mastered: {masterPoints} TM points and {master.ItemCount} of item {master.ItemId} spent");
    }

    private IReadOnlyList<int> MasterableSkills(SkillShopEntity shop)
    {
        if (!_catalogs.MasterLists.TryGetValue(shop.LimitSkillMaster, out var allowed)) return Array.Empty<int>();
        return _player.Skills()
            .Where(skill => skill.Value == MasterableLevel && _catalogs.MasterItems.ContainsKey(skill.Key) && allowed.Contains(skill.Key))
            .Select(skill => skill.Key)
            .OrderBy(skillId => skillId)
            .ToList();
    }

    private async Task RefuseMasterAsync(int skillId, string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   mastering skill {skillId} refused: {reason}");
        await SendAsync(Opcode.S2C_SKILL_MASTER_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    public async Task ChangeJobAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!IsOpen || payload.Length < SkillShopFrames.JobChangeRequestSize) return;
        var npcEntityId = BinaryPrimitives.ReadUInt16LittleEndian(payload.Span);
        if (FindShop(npcEntityId) is not { } shop || _player.Shopper.Character() is not { } character)
        {
            await RefuseForwardedAsync(ct).ConfigureAwait(false);
            return;
        }
        var currentJob = OccupationRules.CurrentJobOf(character);
        if (shop.LimitJobType != 0 && shop.LimitJobType != currentJob)
        {
            await RefuseJobChangeAsync(closes: false, $"job {currentJob} is not {shop.LimitJobType}", ct).ConfigureAwait(false);
            return;
        }
        if (character.ChangeJobQuestState != 0 && character.ChangeJobQuestState != shop.ChangeJob)
        {
            await RefuseJobChangeAsync(closes: false, $"pending job {character.ChangeJobQuestState} is not {shop.ChangeJob}", ct).ConfigureAwait(false);
            return;
        }
        if (shop.JobQuestId != 0 && character.ChangeJobQuestState != shop.ChangeJob)
        {
            await RefuseJobChangeAsync(closes: false, $"job quest {shop.JobQuestId} is not finished", ct).ConfigureAwait(false);
            return;
        }
        if (_player.WornCount is not { } wornCount || _player.FreeBagSlots is not { } freeSlots || wornCount() > freeSlots())
        {
            await RefuseJobChangeAsync(closes: true, "no room in the bag for the equipment", ct).ConfigureAwait(false);
            return;
        }
        if (OccupationRules.TierOf(shop.ChangeType, shop.ChangeJob) is not byte tier || _player.ChangeJob is not { } change)
        {
            await RefuseJobChangeAsync(closes: true, $"no job change from type {shop.ChangeType} to job {shop.ChangeJob}", ct).ConfigureAwait(false);
            return;
        }
        Console.WriteLine($"{_logTag} {_ctx.Remote}   job change at 0x{npcEntityId:X4}: type {shop.ChangeType}, job {shop.ChangeJob}, index {tier}");
        if (!await change(tier, (ushort)shop.ChangeType, (byte)shop.ChangeJob, ct).ConfigureAwait(false))
        {
            await RefuseJobChangeAsync(closes: true, "the Master refused the change", ct).ConfigureAwait(false);
            await SendAsync(Opcode.SKILL_SHOP_CLOSE, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        IsOpen = false;
    }

    private static List<ushort> ReadScriptArgs(ReadOnlySpan<byte> payload)
    {
        var args = new List<ushort>();
        if (payload.Length < SkillShopFrames.ScriptHeaderSize) return args;
        var count = payload[3];
        for (var i = 0; i < count && SkillShopFrames.ScriptHeaderSize + i * 2 + 2 <= payload.Length; i++)
        {
            args.Add(BinaryPrimitives.ReadUInt16LittleEndian(payload[(SkillShopFrames.ScriptHeaderSize + i * 2)..]));
        }
        return args;
    }

    private async Task RunJobQuestAsync(ushort npcEntityId, SkillShopEntity shop, byte function, IReadOnlyList<ushort> args, CancellationToken ct)
    {
        if (_player.JobQuests is not { } quests)
        {
            await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var tier = function >= ThirdJobQuestCheck ? JobQuestRules.ThirdJobQuest : JobQuestRules.SecondJobQuest;
        Task SendPage(ushort index, CancellationToken pageCt) => SendPageAsync(npcEntityId, shop, index, pageCt);
        var result = function switch
        {
            SecondJobQuestCheck or ThirdJobQuestCheck => await quests.CheckAsync(shop, tier, ct).ConfigureAwait(false),
            SecondJobQuestAccept or ThirdJobQuestAccept => await quests.AcceptAsync(shop, args, SendPage, ct).ConfigureAwait(false),
            _ => await quests.CompleteAsync(shop, args, SendPage, ct).ConfigureAwait(false),
        };
        Console.WriteLine($"{_logTag} {_ctx.Remote}   skill master 0x{npcEntityId:X4} job quest function {function}: result {result}");
        if (result == tPoseidon.Core.Game.Quests.NpcScript.Pending) return;
        if (result >= 1 && result <= args.Count)
        {
            await SendPageAsync(npcEntityId, shop, args[result - 1], ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private async Task RunSkillQuestCheckAsync(ushort npcEntityId, SkillShopEntity shop, ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (_player.JobQuests is not { } quests || !NpcParamScriptRequest.TryRead(payload.Span, out var request))
        {
            await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        var result = quests.CheckSkillQuest(
            request.NumberAt(tPoseidon.Core.Game.Quests.SkillQuestRules.QuestParameter),
            request.NumberAt(tPoseidon.Core.Game.Quests.SkillQuestRules.SkillParameter));
        Console.WriteLine($"{_logTag} {_ctx.Remote}   skill master 0x{npcEntityId:X4} skill quest check: result {result}");
        if (result == tPoseidon.Core.Game.Quests.NpcScript.Refused || result > request.Args.Count)
        {
            await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
            return;
        }
        if (result >= 1) await SendPageAsync(npcEntityId, shop, request.Args[result - 1], ct).ConfigureAwait(false);
    }

    private async Task SendPageAsync(ushort npcEntityId, SkillShopEntity shop, ushort index, CancellationToken ct)
    {
        if (index < shop.Pages.Count && !string.IsNullOrWhiteSpace(shop.Pages[index].Text))
        {
            var (kind, text) = shop.Pages[index];
            await SendAsync(Opcode.S2C_SKILL_SHOP_PAGE,
                SkillShopFrames.Page(npcEntityId, text, (ushort)kind, (ushort)shop.JobQuestId, index, _textEncoding), ct).ConfigureAwait(false);
            return;
        }
        if (_player.JobQuests?.QuestMessageOf(shop, index) is { } questText)
        {
            await SendAsync(Opcode.S2C_SKILL_SHOP_PAGE,
                SkillShopFrames.Page(npcEntityId, questText, 0, (ushort)shop.JobQuestId, index, _textEncoding), ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private async Task RefuseJobChangeAsync(bool closes, string reason, CancellationToken ct)
    {
        if (closes) IsOpen = false;
        await _ctx.SendFrameAsync(SkillShopFrames.JobChangeRefused, Array.Empty<byte>(), NextRandKey(), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   job change refused: {reason}");
    }

    private async Task RefuseForwardedAsync(CancellationToken ct)
    {
        IsOpen = false;
        await SendAsync(Opcode.S2C_SKILL_SHOP_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private ShopOffer? OfferById(ushort npcEntityId) => FindShop(npcEntityId) is { } shop ? OfferOf(shop) : null;

    private ShopOffer OfferOf(SkillShopEntity shop) => new()
    {
        NpcEntityId = shop.EntityId,
        Goods = _catalogs.GoodsByTable.TryGetValue(shop.ItemTable, out var goods)
            ? goods.Take(ShopPrices.GoodsLimit).ToList()
            : Array.Empty<ShopGoodsEntry>(),
        PurchaseRate = shop.PurchaseRate,
        SellingRate = shop.SellingRate,
        PriceRate = _catalogs.PriceRateByMap.TryGetValue(_player.Shopper.MapId(), out var rate) ? rate : 0f,
        SellsSkillCards = true,
    };

    private SkillShopEntity? FindShop(ushort npcEntityId) =>
        _catalogs.SkillShops.Get(npcEntityId) is { } shop && shop.MapId == _player.Shopper.MapId() ? shop : null;

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);
}
