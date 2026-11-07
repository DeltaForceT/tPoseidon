using System.Collections.Concurrent;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Trident;

namespace tPoseidon.Kr2014.SystemServer;

public sealed class SocialMember
{
    public SocialMember(TridentSessionContext context, Character character)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        Character = character ?? throw new ArgumentNullException(nameof(character));
        CharacterType = FriendRules.CharacterTypeOf(character);
    }

    public TridentSessionContext Context { get; }

    public Character Character { get; private set; }

    public void Rename(string newName)
    {
        ArgumentNullException.ThrowIfNull(newName);
        Character = Character.WithName(newName);
    }

    public bool FriendLoggedIn { get; set; }

    public ushort? FriendRequestOption { get; set; }

    public bool FriendRequestPending { get; set; }

    public bool GiftOpen { get; set; }

    public ushort? ChatClientId { get; set; }

    public ushort CharacterType { get; set; }

    public ReportSlot?[]? Reports { get; set; }
}

public sealed class SocialRoom
{
    private readonly ConcurrentDictionary<ulong, SocialMember> _members = new();
    private readonly ConcurrentDictionary<TridentSessionContext, ulong> _bySession = new();

    public SocialMember? Enter(SocialMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var id = member.Character.CharacterId;
        SocialMember? displaced = null;
        _members.AddOrUpdate(
            id,
            member,
            (_, existing) =>
            {
                displaced = ReferenceEquals(existing.Context, member.Context) ? null : existing;
                return member;
            });
        if (displaced is not null) _bySession.TryRemove(displaced.Context, out _);
        _bySession[member.Context] = id;
        return displaced;
    }

    public SocialMember? Leave(TridentSessionContext context)
    {
        if (!_bySession.TryRemove(context, out var id)) return null;
        if (!_members.TryGetValue(id, out var member) || !ReferenceEquals(member.Context, context)) return null;
        return _members.TryRemove(new KeyValuePair<ulong, SocialMember>(id, member)) ? member : null;
    }

    public SocialMember? Find(ulong characterId) => _members.TryGetValue(characterId, out var member) ? member : null;

    public IReadOnlyCollection<SocialMember> Members => _members.Values.ToArray();

    public SocialMember? FindNamed(string name) =>
        _members.Values.FirstOrDefault(member => string.Equals(member.Character.Name, name, StringComparison.OrdinalIgnoreCase));

    public SocialMember? Find(TridentSessionContext context) =>
        _bySession.TryGetValue(context, out var id) ? Find(id) : null;
}
