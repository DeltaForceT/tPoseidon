namespace tPoseidon.Core.Net.Social;

public sealed record GameMasterBlockEntry(byte Type, ulong CharacterId, string Name, uint Minutes, string Time);

public sealed class GameMasterBlocks
{
    public const byte Normal = 1;
    public const byte Chat = 2;
    public const byte Trade = 3;
    public const byte Move = 5;
    public const byte MyCamp = 6;
    public const byte PersonalShop = 7;
    public const byte ChatRoom = 8;
    public const uint UnknownMinutes = uint.MaxValue;

    public static readonly byte[] ListedTypes = [Chat, Trade, Move, MyCamp, PersonalShop, ChatRoom];

    private readonly object _gate = new();
    private readonly List<GameMasterBlockEntry> _entries = new();

    public static bool IsType(ushort type) => type is Normal or Chat or Trade or Move or MyCamp or PersonalShop or ChatRoom;

    public void Add(GameMasterBlockEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _entries.RemoveAll(held => held.Type == entry.Type && held.CharacterId == entry.CharacterId);
            _entries.Add(entry);
        }
    }

    public bool Remove(byte type, ulong characterId)
    {
        lock (_gate) return _entries.RemoveAll(held => held.Type == type && held.CharacterId == characterId) > 0;
    }

    public IReadOnlyList<GameMasterBlockEntry> OfType(byte type)
    {
        lock (_gate) return _entries.Where(entry => entry.Type == type).ToArray();
    }

    public IReadOnlyList<GameMasterBlockEntry> Listed()
    {
        lock (_gate) return ListedTypes.SelectMany(type => _entries.Where(entry => entry.Type == type)).ToArray();
    }
}
