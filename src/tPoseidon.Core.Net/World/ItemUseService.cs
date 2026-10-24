using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.Game.Stats;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Core.Net.World;

public sealed record ItemUsePlayer
{
    public required Func<Character?> Character { get; init; }
    public required Func<StatManager?> Stats { get; init; }
    public required Func<ushort> EntityId { get; init; }
    public required Func<uint> MaxHp { get; init; }
    public required Func<uint> MaxMp { get; init; }
    public required Func<bool> IsDead { get; init; }
    public required Func<Opcode, byte[], ushort, Task> SendToMap { get; init; }
    public required Func<int> MaxWeight { get; init; }
    public Func<ItemParamEntry, CancellationToken, Task<bool>>? StarTears { get; init; }
    public Func<int, CancellationToken, Task<bool>>? Disguise { get; init; }
    public Func<Item, ulong, CancellationToken, Task<bool>>? PlantSeed { get; init; }
    public Func<int, Task>? SeedEvent { get; init; }
    public Func<Item, ItemParamEntry, CancellationToken, Task<bool>>? SoulPotion { get; init; }
    public Action<ItemParamEntry>? MonsterCardUsed { get; init; }
    public Func<ItemParamEntry, CancellationToken, Task<bool>>? LearnSkillCard { get; init; }
    public Func<ItemParamEntry, CancellationToken, Task<WarpTicketUse?>>? FiestaTicket { get; init; }
    public Func<ItemParamEntry, CancellationToken, Task<bool>>? TeleportMap { get; init; }
    public Func<ItemParamEntry, CancellationToken, Task<bool>>? TreasureMap { get; init; }
    public Func<ushort, Item, CancellationToken, Task<bool>>? ReviveTarget { get; init; }
}

public sealed class ItemUseService
{
    private readonly S2SessionContext _ctx;
    private readonly IItemStore _items;
    private readonly IItemParamLookup _catalog;
    private readonly ItemUsePlayer _player;
    private readonly CommandBlockIds _blocks;
    private readonly Random _rng;
    private readonly string _logTag;
    private readonly IReadOnlyDictionary<int, PresentItemEntry> _presents;
    private readonly ItemManager _itemManager;

    public ItemUseService(
        S2SessionContext ctx,
        IItemStore items,
        IItemParamLookup catalog,
        ItemUsePlayer player,
        CommandBlockIds blocks,
        Random rng,
        string logTag,
        IReadOnlyDictionary<int, PresentItemEntry> presents,
        ItemManager itemManager)
    {
        _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _player = player ?? throw new ArgumentNullException(nameof(player));
        _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
        _presents = presents ?? throw new ArgumentNullException(nameof(presents));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
    }

    public async Task UseAsync(ReadOnlyMemory<byte> payload, CancellationToken ct)
    {
        if (!ItemUseFrames.TryReadUse(payload.Span, out var wireUid)) return;
        if (_player.IsDead()) return;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return;
        if (Held(character, wireUid) is not { } held)
        {
            await RefuseAsync($"0x{BagItemIds.Stored(wireUid):X} is not in the bag", ct).ConfigureAwait(false);
            return;
        }
        await UseHeldAsync(held.Item, held.Definition, stats, wireUid, ct).ConfigureAwait(false);
    }

    public async Task UseOnTargetAsync(ReadOnlyMemory<byte> payload, Func<ushort, bool> targetExists, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(targetExists);
        if (!ItemUseFrames.TryReadUseOnTarget(payload.Span, out var wireUid, out var targetId)) return;
        if (_player.IsDead()) return;
        if (_player.Character() is not { } character || _player.Stats() is not { } stats) return;
        if (!targetExists(targetId))
        {
            await RefuseAsync($"target 0x{targetId:X4} is not in the zone", ct).ConfigureAwait(false);
            return;
        }
        if (Held(character, wireUid) is not { } held)
        {
            await RefuseAsync($"0x{BagItemIds.Stored(wireUid):X} is not in the bag", ct).ConfigureAwait(false);
            return;
        }
        if (stats.CurrentMp < held.Definition.MpCon || stats.CurrentHp < held.Definition.HpCon)
        {
            await RefuseAsync($"item {held.Item.ItemIndex} on 0x{targetId:X4}: needs MP {held.Definition.MpCon} / HP {held.Definition.HpCon} (has {stats.CurrentMp} / {stats.CurrentHp})", ct).ConfigureAwait(false);
            return;
        }
        if (held.Definition.Effect == ItemParamEntry.RebirthEffect && _player.ReviveTarget is { } revive)
        {
            await ReviveTargetAsync(held.Item, held.Definition, stats, wireUid, targetId, revive, ct).ConfigureAwait(false);
            return;
        }
        await UseHeldAsync(held.Item, held.Definition, stats, wireUid, ct).ConfigureAwait(false);
    }

    private async Task ReviveTargetAsync(
        Item item, ItemParamEntry definition, StatManager stats, ulong wireUid, ushort targetId, Func<ushort, Item, CancellationToken, Task<bool>> revive, CancellationToken ct)
    {
        if (ItemUseRules.Check(definition, stats.Level) is { } refusal)
        {
            await RefuseAsync($"item {item.ItemIndex}: {refusal}", ct).ConfigureAwait(false);
            return;
        }
        if (!await revive(targetId, item, ct).ConfigureAwait(false))
        {
            await RefuseAsync($"rebirth item {item.ItemIndex} on 0x{targetId:X4}: not revived", ct).ConfigureAwait(false);
            return;
        }
        await SendAsync(Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(wireUid), ct).ConfigureAwait(false);
        var left = Consume(item, definition);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   rebirth item {item.ItemIndex} revived 0x{targetId:X4}, {left} left");
    }

    private (Item Item, ItemParamEntry Definition)? Held(Character character, ulong wireUid)
    {
        var uid = BagItemIds.Stored(wireUid);
        var item = _items.GetForCharacterBySlotKind(character.CharacterId, SlotKind.Inventory).FirstOrDefault(held => held.ItemUid == uid);
        return item is not null && _catalog.GetById(item.ItemIndex) is { } definition ? (item, definition) : null;
    }

    private async Task UseHeldAsync(Item item, ItemParamEntry definition, StatManager stats, ulong wireUid, CancellationToken ct)
    {
        if (ItemUseRules.Check(definition, stats.Level) is { } refusal)
        {
            await RefuseAsync($"item {item.ItemIndex}: {refusal}", ct).ConfigureAwait(false);
            return;
        }

        switch (ItemUseRules.HelperOf(definition.Type))
        {
            case ItemUseHelper.NoHelper:
                await SendAsync(Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(wireUid), ct).ConfigureAwait(false);
                Consume(item, definition);
                Console.WriteLine($"{_logTag} {_ctx.Remote}   item {item.ItemIndex} used (type {definition.Type} has no effect)");
                break;
            case ItemUseHelper.Potion:
                await DrinkAsync(item, definition, stats, wireUid, ct).ConfigureAwait(false);
                break;
            case ItemUseHelper.Use when definition.SubType == tPoseidon.Core.Game.SoulSeed.SoulSeedRules.SeedSubType:
                if (_player.PlantSeed is not { } plant || !await plant(item, wireUid, ct).ConfigureAwait(false))
                {
                    await RefuseAsync($"seed {item.ItemIndex} refused", ct).ConfigureAwait(false);
                }
                break;
            case ItemUseHelper.Use when definition.SubType == tPoseidon.Core.Game.Characters.DisguiseRules.KitSubType:
                if (_player.Disguise is not { } disguise || !await disguise(item.ItemIndex, ct).ConfigureAwait(false))
                {
                    await RefuseAsync($"disguise kit {item.ItemIndex} refused", ct).ConfigureAwait(false);
                    break;
                }
                await SendAsync(Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(wireUid), ct).ConfigureAwait(false);
                Consume(item, definition);
                break;
            case ItemUseHelper.Use when definition.SubType == tPoseidon.Core.Game.Soul.SoulRules.PotionSubType && _player.SoulPotion is { } soulPotion:
                if (!await soulPotion(item, definition, ct).ConfigureAwait(false))
                {
                    Console.WriteLine($"{_logTag} {_ctx.Remote}   soul potion {item.ItemIndex} not used");
                }
                break;
            case ItemUseHelper.Use when definition.Effect == tPoseidon.Core.Game.Fiesta.FiestaRules.TicketEffect && _player.FiestaTicket is { } fiestaTicket:
                if (await fiestaTicket(definition, ct).ConfigureAwait(false) is not { } ticketUse)
                {
                    Console.WriteLine($"{_logTag} {_ctx.Remote}   fiesta ticket {item.ItemIndex} refused");
                    break;
                }
                await UseWarpTicketAsync(item, definition, wireUid, ticketUse, ct).ConfigureAwait(false);
                break;
            case ItemUseHelper.Use when definition.Effect == tPoseidon.Core.Game.Drilling.SurpriseSpotRules.TeleportMapEffect && _player.TeleportMap is { } teleportMap:
                if (!await teleportMap(definition, ct).ConfigureAwait(false)) break;
                await SendAsync(Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(wireUid), ct).ConfigureAwait(false);
                Consume(item, definition);
                break;
            case ItemUseHelper.Use when definition.Effect == tPoseidon.Core.Game.Drilling.SurpriseSpotRules.TreasureMapEffect && _player.TreasureMap is { } treasureMap:
                if (!await treasureMap(definition, ct).ConfigureAwait(false)) break;
                await SendAsync(Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(wireUid), ct).ConfigureAwait(false);
                Consume(item, definition);
                break;
            case ItemUseHelper.Use when definition.Effect == PresentRolls.GiftBoxEffect:
                await OpenPresentAsync(item, definition, item.CharacterId, wireUid, ct).ConfigureAwait(false);
                break;
            case ItemUseHelper.Use when definition.Effect == tPoseidon.Core.Game.Fortune.FortuneRules.StarTearsEffect:
                if (_player.StarTears is not { } starTears || !await starTears(definition, ct).ConfigureAwait(false))
                {
                    await RefuseAsync($"item {item.ItemIndex}: no fortune award to extend", ct).ConfigureAwait(false);
                    break;
                }
                await SendAsync(Opcode.S2C_ITEM_USED, ItemUseFrames.Consumed(wireUid), ct).ConfigureAwait(false);
                Consume(item, definition);
                Console.WriteLine($"{_logTag} {_ctx.Remote}   item {item.ItemIndex} extends the fortune award by {definition.Life} h");
                break;
            case ItemUseHelper.SkillCard when definition.SubType == tPoseidon.Core.Game.Skills.SkillCardRules.CardSubType && _player.LearnSkillCard is { } learn:
                if (!await learn(definition, ct).ConfigureAwait(false))
                {
                    await RefuseAsync($"skill card {item.ItemIndex} not learned", ct).ConfigureAwait(false);
                    break;
                }
                await UseSkillCardAsync(item, definition, wireUid, ct).ConfigureAwait(false);
                break;
            case ItemUseHelper.Card when ItemUseRules.IsMonsterCard(definition):
                await UseMonsterCardAsync(item, definition, wireUid, ct).ConfigureAwait(false);
                break;
            case ItemUseHelper.Refuses:
                await RefuseAsync($"item {item.ItemIndex}: type {definition.Type} cannot be used", ct).ConfigureAwait(false);
                break;
            default:
                await RefuseAsync($"item {item.ItemIndex}: type {definition.Type} ({ItemUseRules.HelperOf(definition.Type)}) is not implemented", ct).ConfigureAwait(false);
                break;
        }
    }

    private async Task DrinkAsync(Item item, ItemParamEntry definition, StatManager stats, ulong wireUid, CancellationToken ct)
    {
        var maxHp = _player.MaxHp();
        var maxMp = _player.MaxMp();
        if (ItemUseRules.PotionNotNeeded(definition, stats.CurrentHp, maxHp, stats.CurrentMp, maxMp))
        {
            await RefuseAsync($"item {item.ItemIndex}: {ItemUseRefusal.PotionNotNeeded} (HP {stats.CurrentHp}/{maxHp}, MP {stats.CurrentMp}/{maxMp})", ct).ConfigureAwait(false);
            return;
        }

        var playerId = _player.EntityId();
        var block = _blocks.Next();
        var reply = ItemUseFrames.Reply((ushort)definition.Effect, playerId);
        await SendAsync(Opcode.S2C_ITEM_USE_EFFECT, ItemUseFrames.Used(wireUid, reply), block, ct).ConfigureAwait(false);
        await _player.SendToMap(Opcode.S2C_ITEM_USE_SEEN, ItemUseFrames.Seen(playerId, (uint)item.ItemIndex, reply), block).ConfigureAwait(false);
        var (hp, mp) = stats.RestoreVitals(definition.Hp, maxHp, definition.Mp, maxMp);
        if (definition.Hp != 0)
        {
            await SendAsync(Opcode.S2C_HEALTH_STATUS, new HealthStatusPacket { CurrentHp = hp }.EncodePayload(), block, ct).ConfigureAwait(false);
        }
        if (definition.Mp != 0)
        {
            await SendAsync(Opcode.S2C_MANA_STATUS, new ManaStatusPacket { CurrentMp = mp }.EncodePayload(), block, ct).ConfigureAwait(false);
        }
        await _player.SendToMap(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), block).ConfigureAwait(false);

        var left = item.Stack > 1 ? _items.AdjustStack(item.ItemUid, -1) : Consume(item, definition);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   potion {item.ItemIndex}: HP {hp}/{maxHp}, MP {mp}/{maxMp}, {left} left");
        if (_player.SeedEvent is { } seedEvent)
        {
            if (definition.Hp > 0) await seedEvent(tPoseidon.Core.Game.SoulSeed.SoulSeedRules.HpPotionEvent).ConfigureAwait(false);
            if (definition.Mp > 0) await seedEvent(tPoseidon.Core.Game.SoulSeed.SoulSeedRules.MpPotionEvent).ConfigureAwait(false);
        }
    }

    private async Task UseWarpTicketAsync(Item ticket, ItemParamEntry definition, ulong wireUid, WarpTicketUse use, CancellationToken ct)
    {
        var block = _blocks.Next();
        await SendAsync(Opcode.S2C_ITEM_USE_EFFECT, ItemUseFrames.Used(wireUid, ItemUseFrames.EffectOnly((ushort)definition.Effect)), block, ct).ConfigureAwait(false);
        await _player.SendToMap(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), block).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_ITEM_WARP_NOTICE, ItemUseFrames.Notice(use.Notice, use.Text), ct).ConfigureAwait(false);
        var left = Consume(ticket, definition);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   warp ticket {ticket.ItemIndex} used, {left} left");
        await use.Warp(ct).ConfigureAwait(false);
    }

    private async Task UseSkillCardAsync(Item card, ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        var block = _blocks.Next();
        await SendAsync(Opcode.S2C_ITEM_USE_EFFECT, ItemUseFrames.Used(wireUid, ItemUseFrames.EffectOnly((ushort)definition.Effect)), block, ct).ConfigureAwait(false);
        await _player.SendToMap(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), block).ConfigureAwait(false);
        Consume(card, definition);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   skill card {card.ItemIndex} used");
    }

    private async Task UseMonsterCardAsync(Item card, ItemParamEntry definition, ulong wireUid, CancellationToken ct)
    {
        var block = _blocks.Next();
        await SendAsync(Opcode.S2C_ITEM_USE_EFFECT, ItemUseFrames.Used(wireUid, ItemUseFrames.EffectOnly((ushort)definition.Effect)), block, ct).ConfigureAwait(false);
        await _player.SendToMap(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), block).ConfigureAwait(false);
        await SendAsync(Opcode.S2C_MONSTER_CARD_USED, ItemUseFrames.MonsterCardUsed((uint)card.ItemIndex), ct).ConfigureAwait(false);
        Consume(card, definition);
        _player.MonsterCardUsed?.Invoke(definition);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   monster card {card.ItemIndex} used (SubType {definition.SubType})");
    }

    private async Task OpenPresentAsync(Item box, ItemParamEntry definition, ulong characterId, ulong wireUid, CancellationToken ct)
    {
        if (!_presents.TryGetValue(box.ItemIndex, out var present))
        {
            await RefuseAsync($"gift box {box.ItemIndex}: no {PresentItemEntry.TableName} row", ct).ConfigureAwait(false);
            return;
        }
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var weight = bag.Sum(held => (_catalog.GetById(held.ItemIndex)?.Weight ?? 0) * held.WeightUnits);
        if (weight >= _player.MaxWeight())
        {
            await RefusePresentAsync($"gift box {box.ItemIndex}: bag weight {weight} of {_player.MaxWeight()}", ct).ConfigureAwait(false);
            return;
        }
        IReadOnlyList<PresentGrant> rolled;
        lock (_rng) rolled = PresentRolls.Roll(present, _rng.Next);
        var grants = PresentRolls.Merge(rolled, itemId => _catalog.GetById(itemId) is { IsStackable: true });
        if (grants.Count == 0 || grants.Any(grant => _catalog.GetById(grant.ItemId) is null))
        {
            await RefuseAsync($"gift box {box.ItemIndex}: rolled nothing that is in the item table", ct).ConfigureAwait(false);
            return;
        }
        var newSlots = grants.Count(grant => !(_catalog.GetById(grant.ItemId) is { IsStackable: true } && bag.Any(held => held.ItemIndex == grant.ItemId)));
        if (PickupRules.BagCapacity - bag.Count < newSlots)
        {
            await RefusePresentAsync($"gift box {box.ItemIndex}: {newSlots} free bag slots needed", ct).ConfigureAwait(false);
            return;
        }

        var block = _blocks.Next();
        await SendAsync(Opcode.S2C_ITEM_USE_EFFECT, ItemUseFrames.Used(wireUid, ItemUseFrames.EffectOnly((ushort)definition.Effect)), block, ct).ConfigureAwait(false);
        await _player.SendToMap(Opcode.S2C_COMMAND_BLOCK_END, Array.Empty<byte>(), block).ConfigureAwait(false);
        Consume(box, definition);

        var operations = new List<BagOperation>(grants.Count);
        foreach (var grant in grants)
        {
            var given = _catalog.GetById(grant.ItemId)!;
            var count = (ushort)Math.Clamp(grant.Count, PresentRolls.SmallestCount, ushort.MaxValue);
            var stack = given.IsStackable
                ? _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory).FirstOrDefault(held => held.ItemIndex == grant.ItemId)
                : null;
            if (stack is not null)
            {
                var total = _items.AdjustStack(stack.ItemUid, count);
                operations.Add(new BagOperation(QuestResultFrames.StackChanged, given.ObjectClass, (uint)grant.ItemId, BagItemIds.OnWire(stack.ItemUid), total));
            }
            else
            {
                var minted = _itemManager.GrantRewardItem(characterId, grant.ItemId, count, itemClassOverride: given.ObjectClass);
                operations.Add(new BagOperation(QuestResultFrames.Created, given.ObjectClass, (uint)grant.ItemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot));
            }
        }
        await SendAsync(Opcode.S2C_PRESENT_OPENED, ItemUseFrames.PresentOpened((uint)box.ItemIndex, operations), ct).ConfigureAwait(false);
        Console.WriteLine($"{_logTag} {_ctx.Remote}   gift box {box.ItemIndex} opened: {string.Join(", ", grants.Select(grant => $"{grant.ItemId} x{grant.Count}"))}");
    }

    private async Task RefusePresentAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   item use refused: {reason}");
        await SendAsync(Opcode.S2C_PRESENT_REFUSED, ItemUseFrames.PresentRefused(), ct).ConfigureAwait(false);
    }

    private int Consume(Item item, ItemParamEntry definition)
    {
        if (definition.IsKeptWhenUsed) return item.Stack;
        if (item.Stack > 1) return _items.AdjustStack(item.ItemUid, -1);
        _items.Delete(item.ItemUid);
        return 0;
    }

    private async Task RefuseAsync(string reason, CancellationToken ct)
    {
        Console.WriteLine($"{_logTag} {_ctx.Remote}   item use refused: {reason}");
        await SendAsync(Opcode.S2C_ITEM_USE_REFUSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
    }

    private byte NextRandKey()
    {
        lock (_rng) return (byte)_rng.Next(1, 256);
    }

    private Task SendAsync(Opcode opcode, byte[] payload, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, randKey: NextRandKey(), ct);

    private Task SendAsync(Opcode opcode, byte[] payload, ushort blockId, CancellationToken ct) =>
        _ctx.SendFrameAsync(opcode, payload, NextRandKey(), blockId, ct);
}
