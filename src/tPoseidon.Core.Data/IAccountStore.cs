using System.Net;

namespace tPoseidon.Core.Data;

public interface IAccountStore
{
    Account? GetAccountByUsername(string username);
    Account? GetAccountById(uint accountId);

        Account? AuthenticateAccount(string username, string passwordPlaceholder);

        SessionToken IssueSessionToken(uint accountId, TimeSpan ttl);

        SessionToken? ValidateAndConsumeSessionToken(uint tokenValue);

        SessionToken SeedSessionToken(uint tokenValue, uint accountId, TimeSpan ttl);

    IReadOnlyList<Character> GetCharactersForAccount(uint accountId);

        Character? GetCharacterById(ulong characterId);

        Character? GetCharacterById(uint characterId) => GetCharacterById((ulong)characterId);

        void AddCharacter(Character character);

    bool SetCharacterMap(ulong characterId, ushort mapId);

    bool SetCharacterHome(ulong characterId, ushort homeMapId);

    bool SetCharacterPosition(ulong characterId, ushort mapId, ushort positionX, ushort positionY);

        bool SaveCharacterStats(ulong characterId, CharacterStatsSnapshot snapshot);

        bool DeleteCharacter(ulong characterId, uint ownerAccountId);

        void UpsertAccount(Account account);

    Account RegisterAccount(string username, string passwordHash, string authority = "A");

    bool VerifyPassword(string username, string plaintextPassword);

    void IssueGameAuthcode(uint accountId, long authcode, IPAddress clientIp);

    void TransitionToWorldSelected(uint accountId, IPAddress clientIp, short world, short island, ulong selectedCharUid);

    bool TransitionToServerSelected(uint accountId, short world, short island);

    AccountGameLogin? GetGameLogin(uint accountId);

    short AccountAccess(uint accountId);

    void RefreshLatestIp(uint accountId, IPAddress clientIp);

    PendingGameJoin? FindPendingGameJoin(IPAddress peerIp, TimeSpan window);

    void TransitionToInGame(uint accountId, short island);

    void TransitionToOffline(uint accountId);

    LobbyAccountState? GetLobbyAccountState(uint accountId);

    PenaltyNotice? FindRunningPenalty(uint accountId);

    int ReleaseEndedPenalties();

    int ResetLobbyStates();

    bool EndLobbySession(uint accountId);

    int ResetWorldStates();

    bool JoinGame(uint accountId, long authcode, short island);

    void ResetGameState(uint accountId);
}

public sealed record LobbyAccountState(short AccountAccess, short GameAccess, short State, DateTimeOffset? LatestLoginAt);

public sealed record PenaltyNotice(string Name, DateTimeOffset ReleaseAt);

public sealed record AccountGameLogin(uint AccountId, long Authcode, short State);

public sealed record PendingGameJoin(uint AccountId, long Authcode, short World, short Island, ulong SelectedCharacterId);
