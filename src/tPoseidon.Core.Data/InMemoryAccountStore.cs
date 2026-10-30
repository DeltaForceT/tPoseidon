using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Data;

public sealed class InMemoryAccountStore : IAccountStore
{
    private readonly ConcurrentDictionary<uint, Account> _accountsById = new();
    private readonly ConcurrentDictionary<string, Account> _accountsByUsername =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<ulong, Character> _charactersById = new();
    private readonly ConcurrentDictionary<uint, SessionToken> _sessionTokens = new();
    private readonly ConcurrentDictionary<string, string> _passwordHashesByUsername =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<uint, AccountGameState> _accountGameStates = new();
    private readonly ConcurrentDictionary<uint, short> _accountAccess = new();
    private readonly ConcurrentDictionary<uint, short> _gameAccess = new();
    private readonly ConcurrentDictionary<uint, PenaltyNotice> _penalties = new();

    private long _nextAccountId = 1000;

    private readonly TimeProvider _clock;

    private sealed class AccountGameState
    {
        public long Authcode;
        public short State;
        public DateTimeOffset StateChangedAt;
        public IPAddress? LatestIp;
        public short World;
        public short Island;
        public ulong SelectedCharacterId;
        public DateTimeOffset? LatestLoginAt;
    }

    public InMemoryAccountStore(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

        public void SeedAccount(Account account, IReadOnlyList<Character> characters)
    {
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(characters);

        if (!_accountsById.TryAdd(account.AccountId, account))
        {
            throw new InvalidOperationException(
                $"Account ID {account.AccountId} already seeded.");
        }
        if (!_accountsByUsername.TryAdd(account.Username, account))
        {
            _accountsById.TryRemove(account.AccountId, out _);
            throw new InvalidOperationException(
                $"Username '{account.Username}' already seeded.");
        }
        foreach (var character in characters)
        {
            if (character.OwnerAccountId != account.AccountId)
            {
                throw new InvalidOperationException(
                    $"Character {character.CharacterId} owner ({character.OwnerAccountId}) " +
                    $"does not match account ({account.AccountId}).");
            }
            if (!_charactersById.TryAdd(character.CharacterId, character))
            {
                throw new InvalidOperationException(
                    $"Character ID {character.CharacterId} already seeded.");
            }
        }
    }

    public Account? GetAccountByUsername(string username) =>
        _accountsByUsername.TryGetValue(username, out var account) ? account : null;

    public Account? GetAccountById(uint accountId) =>
        _accountsById.TryGetValue(accountId, out var account) ? account : null;

    public Account? AuthenticateAccount(string username, string passwordPlaceholder)
    {
        var account = GetAccountByUsername(username);
        if (account is null) return null;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(account.PasswordPlaceholder),
            System.Text.Encoding.UTF8.GetBytes(passwordPlaceholder))
            ? account
            : null;
    }

    public SessionToken IssueSessionToken(uint accountId, TimeSpan ttl) =>
        SeedSessionToken(NextTokenValue(), accountId, ttl);

    public SessionToken SeedSessionToken(uint tokenValue, uint accountId, TimeSpan ttl)
    {
        var now = _clock.GetUtcNow();
        var token = new SessionToken
        {
            Value = tokenValue,
            AccountId = accountId,
            IssuedAt = now,
            ExpiresAt = now + ttl,
        };
        _sessionTokens[token.Value] = token;
        return token;
    }

    public SessionToken? ValidateAndConsumeSessionToken(uint tokenValue)
    {
        if (!_sessionTokens.TryRemove(tokenValue, out var token)) return null;
        if (token.IsExpired(_clock.GetUtcNow()))
        {
            return null;
        }
        return token;
    }

    public IReadOnlyList<Character> GetCharactersForAccount(uint accountId)
    {
        var account = GetAccountById(accountId);
        if (account is null) return Array.Empty<Character>();

        return _charactersById.Values
            .Where(c => c.OwnerAccountId == accountId)
            .OrderBy(c => c.Slot)
            .ToList();
    }

    public Character? GetCharacterById(ulong characterId) =>
        _charactersById.TryGetValue(characterId, out var character) ? character : null;

        public void UpsertAccount(Account account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (_accountsById.ContainsKey(account.AccountId)) return;
        SeedAccount(account, Array.Empty<Character>());
    }

        public void AddCharacter(Character character)
    {
        ArgumentNullException.ThrowIfNull(character);

        if (!_accountsById.ContainsKey(character.OwnerAccountId))
        {
            throw new InvalidOperationException(
                $"Cannot add character {character.CharacterId}: " +
                $"owner account {character.OwnerAccountId} does not exist.");
        }
        if (!_charactersById.TryAdd(character.CharacterId, character))
        {
            throw new InvalidOperationException(
                $"Character ID {character.CharacterId} already exists.");
        }
    }

    public bool DeleteCharacter(ulong characterId, uint ownerAccountId)
    {
        if (!_charactersById.TryGetValue(characterId, out var existing)) return false;
        if (existing.OwnerAccountId != ownerAccountId) return false;
        return _charactersById.TryRemove(characterId, out _);
    }

    public bool SetCharacterHome(ulong characterId, ushort homeMapId)
    {
        while (_charactersById.TryGetValue(characterId, out var existing))
        {
            var replacement = InMemoryCharStore.CloneWith(existing, home: homeMapId);
            if (_charactersById.TryUpdate(characterId, replacement, existing)) return true;
        }
        return false;
    }

    public bool SetCharacterMap(ulong characterId, ushort mapId)
    {
        while (_charactersById.TryGetValue(characterId, out var existing))
        {
            var replacement = new Character
            {
                CharacterId = existing.CharacterId,
                OwnerAccountId = existing.OwnerAccountId,
                Slot = existing.Slot,
                Name = existing.Name,
                AccountLabel = existing.AccountLabel,
                Species = existing.Species,
                FType = existing.FType,
                JobTier = existing.JobTier,
                Hair = existing.Hair,
                Money = existing.Money,
                Level = existing.Level,
                StarterEarItemIndex = existing.StarterEarItemIndex,
                StarterTailItemIndex = existing.StarterTailItemIndex,
                PositionX = existing.PositionX,
                PositionY = existing.PositionY,
                MapId = mapId,
                CurrentHp = existing.CurrentHp,
                CurrentMp = existing.CurrentMp,
            };
            if (_charactersById.TryUpdate(characterId, replacement, existing)) return true;
        }
        return false;
    }

    public bool SetCharacterPosition(ulong characterId, ushort mapId, ushort positionX, ushort positionY)
    {
        while (_charactersById.TryGetValue(characterId, out var existing))
        {
            var replacement = new Character
            {
                CharacterId = existing.CharacterId,
                OwnerAccountId = existing.OwnerAccountId,
                Slot = existing.Slot,
                Name = existing.Name,
                AccountLabel = existing.AccountLabel,
                Species = existing.Species,
                FType = existing.FType,
                JobTier = existing.JobTier,
                Hair = existing.Hair,
                Money = existing.Money,
                Level = existing.Level,
                StarterEarItemIndex = existing.StarterEarItemIndex,
                StarterTailItemIndex = existing.StarterTailItemIndex,
                PositionX = positionX,
                PositionY = positionY,
                MapId = mapId,
                CurrentHp = existing.CurrentHp,
                CurrentMp = existing.CurrentMp,
            };
            if (_charactersById.TryUpdate(characterId, replacement, existing)) return true;
        }
        return false;
    }

    public IReadOnlyCollection<string> ForbiddenNames { get; set; } = Array.Empty<string>();

    public uint EditCharacter(ulong characterId, CharacterEdit edit) =>
        InMemoryCharacterEdits.Apply(_charactersById, characterId, edit, ForbiddenNames);

    public bool SaveCharacterStats(ulong characterId, CharacterStatsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        while (_charactersById.TryGetValue(characterId, out var existing))
        {
            var replacement = new Character
            {
                CharacterId = existing.CharacterId,
                OwnerAccountId = existing.OwnerAccountId,
                Slot = existing.Slot,
                Name = existing.Name,
                AccountLabel = existing.AccountLabel,
                Species = existing.Species,
                FType = existing.FType,
                JobTier = existing.JobTier,
                Hair = existing.Hair,
                Money = snapshot.Money,
                Level = snapshot.Level,
                StarterEarItemIndex = existing.StarterEarItemIndex,
                StarterTailItemIndex = existing.StarterTailItemIndex,
                PositionX = existing.PositionX,
                PositionY = existing.PositionY,
                MapId = existing.MapId,
                CurrentHp = snapshot.CurrentHp,
                CurrentMp = snapshot.CurrentMp,
                Exp = snapshot.Exp,
                TmExp = snapshot.TmExp,
                TmLevel = snapshot.TmLevel,
                TmPoint = snapshot.TmPoint,
                ApPoints = snapshot.ApPoints,
                AcPoints = snapshot.AcPoints,
                DxPoints = snapshot.DxPoints,
                MaPoints = snapshot.MaPoints,
                MdPoints = snapshot.MdPoints,
                DaPoints = snapshot.DaPoints,
                LkPoints = snapshot.LkPoints,
                DpPoints = snapshot.DpPoints,
                HvPoints = snapshot.HvPoints,
                MaxHpPoints = snapshot.MaxHpPoints,
                MaxMpPoints = snapshot.MaxMpPoints,
                MaxWtPoints = snapshot.MaxWtPoints,
                AttrPoints = snapshot.AttrPoints,
                TmLevelPower = snapshot.TmLevelPower,
                TmLevelMagic = snapshot.TmLevelMagic,
                TmLevelSense = snapshot.TmLevelSense,
                TmLevelCharm = snapshot.TmLevelCharm,
            };
            if (_charactersById.TryUpdate(characterId, replacement, existing)) return true;
        }
        return false;
    }

    private static uint NextTokenValue()
    {
        Span<byte> buf = stackalloc byte[4];
        RandomNumberGenerator.Fill(buf);
        return BitConverter.ToUInt32(buf);
    }

    public Account RegisterAccount(string username, string passwordHash, string authority = "A")
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(passwordHash);

        var newId = (uint)Interlocked.Increment(ref _nextAccountId);
        var account = new Account
        {
            AccountId = newId,
            Username = username,
            PasswordPlaceholder = string.Empty,
            CharacterIds = Array.Empty<ulong>(),
        };
        if (!_accountsByUsername.TryAdd(username, account))
        {
            throw new InvalidOperationException($"Username '{username}' already exists.");
        }
        if (!_accountsById.TryAdd(newId, account))
        {
            _accountsByUsername.TryRemove(username, out _);
            throw new InvalidOperationException($"Account ID {newId} collision.");
        }
        _passwordHashesByUsername[username] = passwordHash;
        return account;
    }

    public bool VerifyPassword(string username, string plaintextPassword)
    {
        ArgumentException.ThrowIfNullOrEmpty(username);
        ArgumentException.ThrowIfNullOrEmpty(plaintextPassword);

        if (!_passwordHashesByUsername.TryGetValue(username, out var hash)) return false;
        return PasswordHasher.Verify(plaintextPassword, hash);
    }

    public void IssueGameAuthcode(uint accountId, long authcode, IPAddress clientIp)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        var state = _accountGameStates.GetOrAdd(accountId, _ => new AccountGameState());
        lock (state)
        {
            state.Authcode = authcode;
            state.SelectedCharacterId = 0;
            state.State = 2;
            state.StateChangedAt = _clock.GetUtcNow();
            state.LatestLoginAt = state.StateChangedAt;
            state.LatestIp = clientIp;
        }
    }

    public void TransitionToWorldSelected(uint accountId, IPAddress clientIp, short world, short island, ulong selectedCharUid)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        var state = _accountGameStates.GetOrAdd(accountId, _ => new AccountGameState());
        lock (state)
        {
            if (state.State != 4) return;
            state.StateChangedAt = _clock.GetUtcNow();
            state.LatestIp = clientIp;
            state.World = world;
            state.Island = island;
            state.SelectedCharacterId = selectedCharUid;
        }
    }

    public bool TransitionToServerSelected(uint accountId, short world, short island)
    {
        if (!_accountGameStates.TryGetValue(accountId, out var state)) return false;
        lock (state)
        {
            if (state.State != 2) return false;
            state.State = 3;
            state.StateChangedAt = _clock.GetUtcNow();
            state.World = world;
            state.Island = island;
            return true;
        }
    }

    public AccountGameLogin? GetGameLogin(uint accountId)
    {
        if (!_accountsById.ContainsKey(accountId)) return null;
        if (!_accountGameStates.TryGetValue(accountId, out var state)) return new AccountGameLogin(accountId, 0, 0);
        lock (state)
        {
            return new AccountGameLogin(accountId, state.Authcode, state.State);
        }
    }

    public short AccountAccess(uint accountId) => _accountAccess.TryGetValue(accountId, out var access) ? access : (short)0;

    public void SetAccountAccess(uint accountId, short access) => _accountAccess[accountId] = access;

    public void RefreshLatestIp(uint accountId, IPAddress clientIp)
    {
        ArgumentNullException.ThrowIfNull(clientIp);
        var state = _accountGameStates.GetOrAdd(accountId, _ => new AccountGameState());
        lock (state)
        {
            state.LatestIp = clientIp;
        }
    }

    public PendingGameJoin? FindPendingGameJoin(IPAddress peerIp, TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(peerIp);
        var cutoff = _clock.GetUtcNow() - window;

        foreach (var kvp in _accountGameStates)
        {
            var state = kvp.Value;
            lock (state)
            {
                if (state.State != 4 || state.SelectedCharacterId == 0) continue;
                if (state.LatestIp is null || !state.LatestIp.Equals(peerIp)) continue;
                if (state.StateChangedAt < cutoff) continue;
                return new PendingGameJoin(kvp.Key, state.Authcode, state.World, state.Island, state.SelectedCharacterId);
            }
        }
        return null;
    }

    public void TransitionToInGame(uint accountId, short island)
    {
        if (!_accountGameStates.TryGetValue(accountId, out var state))
        {
            throw new InvalidOperationException(
                $"Account {accountId} has no game-state row; cannot transition to in-game.");
        }
        lock (state)
        {
            if (state.State != 4 || state.SelectedCharacterId == 0)
            {
                throw new InvalidOperationException(
                    $"Account {accountId} has no pending character in state 4; cannot enter the game.");
            }

            state.SelectedCharacterId = 0;
            state.Island = island;
        }
    }

    public void TransitionToOffline(uint accountId)
    {
        if (!_accountGameStates.TryGetValue(accountId, out var state)) return;
        lock (state)
        {
            state.State = 0;
            state.StateChangedAt = _clock.GetUtcNow();
        }
    }

    public void SetGameAccess(uint accountId, short access) => _gameAccess[accountId] = access;

    public void SetRunningPenalty(uint accountId, PenaltyNotice? penalty)
    {
        if (penalty is null)
        {
            _penalties.TryRemove(accountId, out _);
            return;
        }

        _penalties[accountId] = penalty;
    }

    public LobbyAccountState? GetLobbyAccountState(uint accountId)
    {
        if (!_accountsById.ContainsKey(accountId)) return null;
        var access = AccountAccess(accountId);
        var gameAccess = _gameAccess.TryGetValue(accountId, out var value) ? value : (short)0;
        if (!_accountGameStates.TryGetValue(accountId, out var state)) return new LobbyAccountState(access, gameAccess, 0, null);
        lock (state)
        {
            var pendingJoin = state.State == 4 && state.SelectedCharacterId != 0 ? state.StateChangedAt : (DateTimeOffset?)null;
            return new LobbyAccountState(access, gameAccess, state.State, state.LatestLoginAt, pendingJoin);
        }
    }

    public PenaltyNotice? FindRunningPenalty(uint accountId) =>
        _penalties.TryGetValue(accountId, out var penalty) ? penalty : null;

    public int ReleaseEndedPenalties()
    {
        var now = _clock.GetUtcNow();
        var released = 0;
        foreach (var entry in _penalties)
        {
            if (entry.Value.ReleaseAt > now || !_penalties.TryRemove(entry.Key, out _)) continue;
            _gameAccess[entry.Key] = 0;
            released++;
        }

        return released;
    }

    public int ResetLobbyStates() => ResetStates(state => state.State is 2 or 3);

    public bool EndLobbySession(uint accountId)
    {
        if (!_accountGameStates.TryGetValue(accountId, out var state)) return false;
        lock (state)
        {
            if (state.State != 2) return false;
            state.State = 0;
            state.StateChangedAt = _clock.GetUtcNow();
            return true;
        }
    }

    public int ResetWorldStates(short world, short island) => ResetStates(state => state.State == 4 && state.World == world && state.Island == island);

    public bool JoinGame(uint accountId, long authcode, short island)
    {
        if (!_accountGameStates.TryGetValue(accountId, out var state)) return false;
        lock (state)
        {
            if (state.State != 3 || state.Authcode != authcode) return false;
            state.State = 4;
            state.StateChangedAt = _clock.GetUtcNow();
            state.Island = island;
            return true;
        }
    }

    public void ResetGameState(uint accountId)
    {
        if (!_accountGameStates.TryGetValue(accountId, out var state)) return;
        lock (state)
        {
            state.State = 0;
            state.StateChangedAt = _clock.GetUtcNow();
        }
    }

    private int ResetStates(Func<AccountGameState, bool> matches)
    {
        var reset = 0;
        foreach (var state in _accountGameStates.Values)
        {
            lock (state)
            {
                if (!matches(state)) continue;
                state.State = 0;
                state.SelectedCharacterId = 0;
                state.StateChangedAt = _clock.GetUtcNow();
                reset++;
            }
        }

        return reset;
    }
}
