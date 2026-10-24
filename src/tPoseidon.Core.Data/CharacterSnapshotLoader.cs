namespace tPoseidon.Core.Data;

public sealed class CharacterSnapshotLoader
{
    private readonly ICharStore _chars;
    private readonly IItemStore _items;
    private readonly ISkillStore _skills;
    private readonly IQuestStore _quests;
    private readonly IUniqueCharDutyInfoStore? _uniqueDuty;
    private readonly IAwakeningStore? _awakening;
    private readonly ICoupleStore? _couple;
    private readonly IPkInfoStore? _pkInfo;
    private readonly IAccountBlockStore? _accountBlocks;

    public CharacterSnapshotLoader(
        ICharStore chars,
        IItemStore items,
        ISkillStore skills,
        IQuestStore quests,
        IUniqueCharDutyInfoStore? uniqueDuty = null,
        IAwakeningStore? awakening = null,
        ICoupleStore? couple = null,
        IPkInfoStore? pkInfo = null,
        IAccountBlockStore? accountBlocks = null)
    {
        ArgumentNullException.ThrowIfNull(chars);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(skills);
        ArgumentNullException.ThrowIfNull(quests);
        _chars = chars;
        _items = items;
        _skills = skills;
        _quests = quests;
        _uniqueDuty = uniqueDuty;
        _awakening = awakening;
        _couple = couple;
        _pkInfo = pkInfo;
        _accountBlocks = accountBlocks;
    }

    public CharacterSnapshot? Load(ulong characterId)
    {
        var character = _chars.GetCharacterById(characterId);
        if (character is null) return null;

        var allItems = _items.GetAllForCharacter(characterId);
        var equipped = allItems.Where(i => i.SlotKind == SlotKind.Equipment).ToArray();
        var inventory = allItems.Where(i => i.SlotKind == SlotKind.Inventory).ToArray();

        var skills = _skills.GetForCharacter(characterId);
        var quests = _quests.GetActiveForCharacter(characterId);

        var uniqueDuty = _uniqueDuty?.GetOrCreate(characterId);
        var awakening = _awakening?.GetOrCreate(characterId);
        var coupleChar = _couple?.FindCharRecord(characterId);
        var pkInfo = _pkInfo?.GetOrCreate(characterId);

        AccountBlock? activeBlock = null;
        if (_accountBlocks is not null)
        {
            var blocks = _accountBlocks.GetActiveBlocks(character.OwnerAccountId, DateTimeOffset.UtcNow);
            activeBlock = blocks.Count > 0 ? blocks[0] : null;
        }

        return new CharacterSnapshot
        {
            Character = character,
            EquippedItems = equipped,
            InventoryItems = inventory,
            Skills = skills,
            ActiveQuests = quests,
            UniqueDutyInfo = uniqueDuty,
            Awakening = awakening,
            CoupleCode = coupleChar?.CoupleCode ?? 0,
            PkInfo = pkInfo,
            ActiveBlock = activeBlock,
        };
    }
}
