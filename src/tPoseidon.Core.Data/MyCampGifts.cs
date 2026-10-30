namespace tPoseidon.Core.Data;

public sealed record MyCampOwner(string CharacterName, uint AccountId);

public sealed record MyCampStock(MyCampOwner Owner, int NewItems, int StoredItems);

public interface IMyCampGifts
{
    MyCampOwner? Find(string characterName);

    MyCampStock Stock(MyCampOwner owner);

    void Give(MyCampOwner owner, int itemIndex, int count);
}

public sealed class MyCampGifts : IMyCampGifts
{
    public const int MinItemIndex = 1;
    public const int MaxItemIndex = ushort.MaxValue;
    public const int MinCount = 1;
    public const int MaxCount = 100;

    private readonly IMyCampStore _camp;
    private readonly ICharacterDirectory _directory;
    private readonly ICharStore _characters;

    public MyCampGifts(IMyCampStore camp, ICharacterDirectory directory, ICharStore characters)
    {
        _camp = camp ?? throw new ArgumentNullException(nameof(camp));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _characters = characters ?? throw new ArgumentNullException(nameof(characters));
    }

    public static bool IsItemIndex(int value) => value is >= MinItemIndex and <= MaxItemIndex;

    public static bool IsCount(int value) => value is >= MinCount and <= MaxCount;

    public MyCampOwner? Find(string characterName)
    {
        ArgumentNullException.ThrowIfNull(characterName);
        return _directory.FindCharacterId(characterName) is { } id && _characters.GetCharacterById(id) is { } character
            ? new MyCampOwner(character.Name, character.OwnerAccountId)
            : null;
    }

    public MyCampStock Stock(MyCampOwner owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        var items = _camp.GetItemsForAccount(owner.AccountId).Where(item => item.ItemWhere != MyCampItemWhere.Deleted).ToArray();
        return new MyCampStock(
            owner,
            items.Count(item => item.ItemWhere == MyCampItemWhere.New),
            items.Count(item => item.ItemWhere == MyCampItemWhere.Inventory));
    }

    public void Give(MyCampOwner owner, int itemIndex, int count)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentOutOfRangeException.ThrowIfLessThan(itemIndex, MinItemIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(itemIndex, MaxItemIndex);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, MinCount);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxCount);
        for (var granted = 0; granted < count; granted++)
        {
            _camp.AddItemForAccount(owner.AccountId, itemIndex);
        }
    }
}
