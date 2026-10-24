namespace tPoseidon.Core.Data;

public sealed class InMemoryFriendStore : IFriendStore
{
    private readonly Func<ulong, Character?> _characterOf;
    private readonly Func<string, Character?> _characterNamed;
    private readonly object _gate = new();
    private readonly Dictionary<(ulong Owner, ulong Friend), (byte Group, bool Deleted)> _friends = new();
    private readonly Dictionary<(ulong Owner, byte Group), string> _groups = new();
    private readonly List<(ulong Owner, FriendMessage Message)> _messages = new();

    public InMemoryFriendStore(Func<ulong, Character?> characterOf, Func<string, Character?>? characterNamed = null)
    {
        _characterOf = characterOf ?? throw new ArgumentNullException(nameof(characterOf));
        _characterNamed = characterNamed ?? (_ => null);
    }

    public FriendEntry? FindCharacter(string name) =>
        _characterNamed(name) is { } character
            ? new FriendEntry(character.CharacterId, 0, character.Name, (ushort)character.Species)
            : null;

    public IReadOnlyList<FriendGroup> GetGroups(ulong ownerCharacterId)
    {
        lock (_gate)
        {
            return _groups.Where(g => g.Key.Owner == ownerCharacterId)
                .OrderBy(g => g.Key.Group)
                .Select(g => new FriendGroup(g.Key.Group, g.Value))
                .ToArray();
        }
    }

    public void CreateDefaultGroups(ulong ownerCharacterId)
    {
        lock (_gate)
        {
            for (var i = 0; i < FriendRules.DefaultGroupNames.Count; i++)
            {
                _groups[(ownerCharacterId, (byte)i)] = FriendRules.DefaultGroupNames[i];
            }
        }
    }

    public bool RenameGroup(ulong ownerCharacterId, byte groupNum, string groupName)
    {
        lock (_gate)
        {
            if (!_groups.ContainsKey((ownerCharacterId, groupNum))) return false;
            _groups[(ownerCharacterId, groupNum)] = groupName;
            return true;
        }
    }

    public IReadOnlyList<FriendEntry> GetFriends(ulong ownerCharacterId)
    {
        lock (_gate)
        {
            return _friends.Where(f => f.Key.Owner == ownerCharacterId && !f.Value.Deleted)
                .Select(f => (f, character: _characterOf(f.Key.Friend)))
                .Where(x => x.character is not null)
                .Select(x => new FriendEntry(x.f.Key.Friend, x.f.Value.Group, x.character!.Name, FriendRules.CharacterTypeOf(x.character)))
                .ToArray();
        }
    }

    public FriendAddResult AddFriend(ulong ownerCharacterId, ulong friendCharacterId, byte groupNum)
    {
        lock (_gate)
        {
            if (_friends.ContainsKey((ownerCharacterId, friendCharacterId))) return FriendAddResult.AlreadyFriends;
            _friends[(ownerCharacterId, friendCharacterId)] = (groupNum, false);
            _friends[(friendCharacterId, ownerCharacterId)] = (groupNum, false);
            return FriendAddResult.Added;
        }
    }

    public bool SetFriendGroup(ulong ownerCharacterId, ulong friendCharacterId, byte groupNum)
    {
        lock (_gate)
        {
            if (!_friends.TryGetValue((ownerCharacterId, friendCharacterId), out var row)) return false;
            _friends[(ownerCharacterId, friendCharacterId)] = (groupNum, row.Deleted);
            return true;
        }
    }

    public FriendRemoveResult RemoveFriend(ulong ownerCharacterId, ulong friendCharacterId, bool friendOnline)
    {
        lock (_gate)
        {
            if (!_friends.Remove((ownerCharacterId, friendCharacterId))) return FriendRemoveResult.NotFriends;
            if (!_friends.TryGetValue((friendCharacterId, ownerCharacterId), out var reverse)) return FriendRemoveResult.ReverseMissing;
            if (friendOnline) _friends.Remove((friendCharacterId, ownerCharacterId));
            else _friends[(friendCharacterId, ownerCharacterId)] = (reverse.Group, true);
            return FriendRemoveResult.Removed;
        }
    }

    public IReadOnlyList<string> TakeDeletedNotifications(ulong ownerCharacterId)
    {
        lock (_gate)
        {
            var deleted = _friends.Where(f => f.Key.Owner == ownerCharacterId && f.Value.Deleted).Select(f => f.Key).ToArray();
            foreach (var key in deleted) _friends.Remove(key);
            return deleted.Select(key => _characterOf(key.Friend)?.Name).OfType<string>().ToArray();
        }
    }

    public int CountMessages(ulong ownerCharacterId)
    {
        lock (_gate) return _messages.Count(m => m.Owner == ownerCharacterId);
    }

    public bool SendMessage(ulong ownerCharacterId, ulong senderCharacterId, byte kind, string content)
    {
        lock (_gate)
        {
            if (_messages.Count(m => m.Owner == ownerCharacterId) >= FriendRules.MostStoredMessages) return false;
            var senderName = senderCharacterId == FriendRules.GmSenderCharacterId
                ? FriendRules.GmSenderName
                : _characterOf(senderCharacterId)?.Name ?? string.Empty;
            _messages.Add((ownerCharacterId, new FriendMessage(senderCharacterId, senderName, DateTimeOffset.UtcNow, content, kind)));
            return true;
        }
    }

    public IReadOnlyList<FriendMessage> TakeMessages(ulong ownerCharacterId)
    {
        lock (_gate)
        {
            var mine = _messages.Where(m => m.Owner == ownerCharacterId).Select(m => m.Message).OrderByDescending(m => m.Kind).ToArray();
            _messages.RemoveAll(m => m.Owner == ownerCharacterId);
            return mine;
        }
    }

    public void RemoveAllFor(ulong characterId)
    {
        lock (_gate)
        {
            foreach (var key in _friends.Keys.Where(k => k.Owner == characterId || k.Friend == characterId).ToArray()) _friends.Remove(key);
            _messages.RemoveAll(m => m.Owner == characterId);
        }
    }
}
