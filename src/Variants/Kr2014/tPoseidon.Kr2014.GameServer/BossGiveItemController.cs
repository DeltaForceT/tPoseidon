using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Items;
using tPoseidon.Core.Game.Monsters;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

internal sealed class BossGiveItemController
{
    private readonly BossGiveItemRoller _roller;
    private readonly IItemStore _items;
    private readonly ItemManager _itemManager;
    private readonly IItemParamLookup _catalog;
    private readonly int _bagCapacity;
    private readonly string _logTag;
    private readonly Func<MonsterEntity, byte[], Task> _announce;
    private readonly Random _rng = new();

    public BossGiveItemController(
        BossGiveItemRoller roller, IItemStore items, ItemManager itemManager, IItemParamLookup catalog, int bagCapacity, Func<MonsterEntity, byte[], Task> announce, string logTag)
    {
        _roller = roller ?? throw new ArgumentNullException(nameof(roller));
        _items = items ?? throw new ArgumentNullException(nameof(items));
        _itemManager = itemManager ?? throw new ArgumentNullException(nameof(itemManager));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _bagCapacity = bagCapacity;
        _announce = announce ?? throw new ArgumentNullException(nameof(announce));
        _logTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
    }

    public async Task GiveAsync(MonsterEntity monster, IReadOnlyCollection<InGameSessionState> players, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(monster);
        ArgumentNullException.ThrowIfNull(players);
        if (!_roller.Gives(monster)) return;
        foreach (var attackerId in monster.TopAttackers(BossGiveItemRoller.MostRecipients))
        {
            var player = players.FirstOrDefault(candidate => candidate.PlayerEntityId == attackerId && candidate.Character is not null);
            if (player?.Character is not { } character) continue;
            int? rolled;
            lock (_rng) rolled = _roller.Roll(monster.MonsterParamId, _rng);
            if (rolled is not int itemId) continue;
            if (Grant(character.CharacterId, itemId) is not { } operation)
            {
                Console.WriteLine($"{_logTag} [boss-give] {character.Name}: bag full, item {itemId} of boss {monster.MonsterParamId} not given");
                continue;
            }
            byte randKey;
            lock (_rng) randKey = (byte)_rng.Next(1, 256);
            await player.SendFrameAsync(Opcode.S2C_ITEM_ADDED, QuestResultFrames.ItemOperations(new[] { operation }), randKey, ct).ConfigureAwait(false);
            await _announce(monster, BossGiveItemFrames.Given(monster.EntityId, (uint)itemId, player.PlayerEntityId)).ConfigureAwait(false);
            Console.WriteLine($"{_logTag} [boss-give] {character.Name} gets item {itemId} for damaging boss {monster.MonsterParamId}");
        }
    }

    private BagOperation? Grant(ulong characterId, int itemId)
    {
        var definition = _catalog.GetById(itemId);
        var itemClass = definition?.ObjectClass ?? (byte)ItemParamEntry.StackableClass;
        var bag = _items.GetForCharacterBySlotKind(characterId, SlotKind.Inventory);
        var stack = definition is { IsStackable: true } ? bag.FirstOrDefault(item => item.ItemIndex == itemId) : null;
        if (stack is not null)
        {
            var total = _items.AdjustStack(stack.ItemUid, 1);
            return new BagOperation(QuestResultFrames.StackChanged, itemClass, (uint)itemId, BagItemIds.OnWire(stack.ItemUid), total);
        }
        if (bag.Count >= _bagCapacity) return null;
        var minted = _itemManager.GrantRewardItem(characterId, itemId, 1, itemClassOverride: itemClass);
        return new BagOperation(QuestResultFrames.Created, itemClass, (uint)itemId, BagItemIds.OnWire(minted.ItemUid), minted.Stack, minted.ItemSlot);
    }
}
