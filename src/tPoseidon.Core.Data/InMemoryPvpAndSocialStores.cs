namespace tPoseidon.Core.Data;

public sealed class InMemoryPvpStore : IPvpStore
{
    private readonly object _gate = new();
    private readonly List<PvpMatch> _matches = new();
    private readonly Dictionary<ulong, PvpLadder> _ladders = new();
    private long _nextMatchSeq = 1;

    public PvpMatch RecordMatch(PvpMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        lock (_gate)
        {
            var record = match with { MatchSeq = _nextMatchSeq++ };
            _matches.Add(record);
            return record;
        }
    }

    public IReadOnlyList<PvpMatch> GetMatchesForCharacter(ulong characterId, int limit = 50)
    {
        lock (_gate)
        {
            return _matches.Where(m => m.CharacterId == characterId)
                .OrderByDescending(m => m.MatchEnd)
                .Take(limit)
                .ToArray();
        }
    }

    public PvpLadder GetOrCreateLadder(ulong characterId)
    {
        lock (_gate)
        {
            if (_ladders.TryGetValue(characterId, out var l)) return l;
            var fresh = new PvpLadder { CharacterId = characterId };
            _ladders[characterId] = fresh;
            return fresh;
        }
    }

    public PvpLadder UpsertLadder(PvpLadder ladder)
    {
        ArgumentNullException.ThrowIfNull(ladder);
        lock (_gate) { _ladders[ladder.CharacterId] = ladder; return ladder; }
    }

    public bool DeleteLadder(ulong characterId)
    {
        lock (_gate) { return _ladders.Remove(characterId); }
    }
}

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

public sealed class InMemoryAccountBlockStore : IAccountBlockStore
{
    private readonly object _gate = new();
    private readonly List<AccountBlock> _blocks = new();
    private long _nextSeq = 1;

    public AccountBlock Add(AccountBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        lock (_gate)
        {
            var record = block with { BlockSeq = _nextSeq++, CreatedAt = DateTimeOffset.UtcNow };
            _blocks.Add(record);
            return record;
        }
    }

    public IReadOnlyList<AccountBlock> GetActiveBlocks(uint targetAccountId, DateTimeOffset now)
    {
        lock (_gate)
        {
            return _blocks.Where(b => b.TargetAccountId == targetAccountId && b.BlockTo > now)
                .OrderByDescending(b => b.BlockSeq)
                .ToArray();
        }
    }

    public IReadOnlyList<AccountBlock> GetAllBlocks(uint targetAccountId, int limit = 50)
    {
        lock (_gate)
        {
            return _blocks.Where(b => b.TargetAccountId == targetAccountId)
                .OrderByDescending(b => b.BlockSeq)
                .Take(limit)
                .ToArray();
        }
    }

    public bool RemoveBlock(long blockSeq)
    {
        lock (_gate)
        {
            var idx = _blocks.FindIndex(b => b.BlockSeq == blockSeq);
            if (idx < 0) return false;
            _blocks.RemoveAt(idx);
            return true;
        }
    }
}

public sealed class InMemoryAccountLogoutStore : IAccountLogoutStore
{
    private readonly object _gate = new();
    private readonly List<AccountLogoutEntry> _entries = new();
    private long _nextSeq = 1;

    public AccountLogoutEntry Append(uint accountId, DateTimeOffset loginAt, DateTimeOffset logoutAt, byte logoutWorld, int? playTime)
    {
        lock (_gate)
        {
            var entry = new AccountLogoutEntry
            {
                LogSeq = _nextSeq++,
                AccountId = accountId,
                LoginAt = loginAt,
                LogoutAt = logoutAt,
                LogoutWorld = logoutWorld,
                PlayTime = playTime,
            };
            _entries.Add(entry);
            return entry;
        }
    }

    public IReadOnlyList<AccountLogoutEntry> GetForAccount(uint accountId, int limit = 50)
    {
        lock (_gate)
        {
            return _entries.Where(e => e.AccountId == accountId)
                .OrderByDescending(e => e.LogSeq)
                .Take(limit)
                .ToArray();
        }
    }
}

public sealed class InMemoryFortuneStore : IFortuneStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, FortuneInfo> _byChar = new();
    private readonly List<FortuneInfoLogEntry> _log = new();
    private long _nextLogSeq = 1;

    public FortuneInfo GetOrCreate(ulong characterId)
    {
        lock (_gate)
        {
            if (_byChar.TryGetValue(characterId, out var f)) return f;
            var now = DateTimeOffset.UtcNow;
            var fresh = new FortuneInfo
            {
                CharacterId = characterId,
                UpdatedAt = now,
                ExpiresAt = now.AddDays(1),
            };
            _byChar[characterId] = fresh;
            return fresh;
        }
    }

    public FortuneInfo? Find(ulong characterId)
    {
        lock (_gate)
        {
            return _byChar.TryGetValue(characterId, out var found) ? found : null;
        }
    }

    public FortuneInfo Upsert(FortuneInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        lock (_gate)
        {
            var record = info with { UpdatedAt = DateTimeOffset.UtcNow };
            _byChar[info.CharacterId] = record;
            return record;
        }
    }

    public void Update(FortuneInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        lock (_gate)
        {
            _byChar[info.CharacterId] = info;
        }
        AppendLog(info);
    }

    public void AppendLog(FortuneInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        lock (_gate)
        {
            _log.Add(new FortuneInfoLogEntry
            {
                LogSeq = _nextLogSeq++,
                CharacterId = info.CharacterId,
                ViewCount = info.ViewCount,
                TitleId = info.TitleId,
                ItemAttrString = info.ItemAttrString,
                LoggedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    public IReadOnlyList<FortuneInfoLogEntry> GetLog(ulong characterId, int limit = 50)
    {
        lock (_gate)
        {
            return _log.Where(e => e.CharacterId == characterId)
                .OrderByDescending(e => e.LogSeq)
                .Take(limit)
                .ToArray();
        }
    }

    public bool Delete(ulong characterId)
    {
        lock (_gate) { return _byChar.Remove(characterId); }
    }
}

public sealed class InMemoryBroadcastStore : IBroadcastStore
{
    private readonly object _gate = new();
    private readonly Dictionary<long, BroadcastMessage> _active = new();
    private readonly List<BroadcastLogEntry> _log = new();
    private long _nextBroadcastCode = 1;
    private long _nextLogSeq = 1;

    public BroadcastMessage AddMessage(BroadcastMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            var record = message with { BroadcastCode = _nextBroadcastCode++, CreatedAt = DateTimeOffset.UtcNow };
            _active[record.BroadcastCode] = record;
            return record;
        }
    }

    public IReadOnlyList<BroadcastMessage> GetActiveForIsland(byte islandCode, int limit = 50)
    {
        lock (_gate)
        {
            return _active.Values.Where(m => m.IslandCode == islandCode)
                .OrderByDescending(m => m.BroadcastCode)
                .Take(limit)
                .ToArray();
        }
    }

    public bool DeleteMessage(long broadcastCode)
    {
        lock (_gate) { return _active.Remove(broadcastCode); }
    }

    public void AppendLog(BroadcastMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        lock (_gate)
        {
            _log.Add(new BroadcastLogEntry
            {
                LogSeq = _nextLogSeq++,
                CharacterId = message.CharacterId,
                IslandCode = message.IslandCode,
                Message = message.Message,
                BroadcastType = message.BroadcastType,
                ColorType = message.ColorType,
                LoggedAt = DateTimeOffset.UtcNow,
            });
        }
    }

    public IReadOnlyList<BroadcastLogEntry> GetLog(ulong characterId, int limit = 50)
    {
        lock (_gate)
        {
            return _log.Where(e => e.CharacterId == characterId)
                .OrderByDescending(e => e.LogSeq)
                .Take(limit)
                .ToArray();
        }
    }
}
