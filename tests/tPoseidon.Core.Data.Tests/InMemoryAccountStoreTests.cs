using tPoseidon.Core.Data;

namespace tPoseidon.Core.Data.Tests;

public class InMemoryAccountStoreTests
{
    private static (Account, IReadOnlyList<Character>) MakeTestAccount(
        uint accountId, string username, params string[] characterNames)
    {
        var charIds = characterNames.Select((_, i) => (ulong)(accountId * 1000 + i)).ToArray();
        var account = new Account
        {
            AccountId = accountId,
            Username = username,
            PasswordPlaceholder = "p" + accountId,
            CharacterIds = charIds,
        };
        var characters = charIds.Select((id, i) => new Character
        {
            CharacterId = id,
            OwnerAccountId = accountId,
            Slot = (byte)i,
            Name = characterNames[i],
            Species = Species.Buffalo,
            FType = FType.Power,
        }).ToList();
        return (account, characters);
    }

    [Fact]
    public void Seed_ThenLookup_ByIdAndUsername()
    {
        var store = new InMemoryAccountStore();
        var (account, chars) = MakeTestAccount(1, "alice", "Char1", "Char2");
        store.SeedAccount(account, chars);

        Assert.Equal(account, store.GetAccountById(1));
        Assert.Equal(account, store.GetAccountByUsername("alice"));
        Assert.Equal(account, store.GetAccountByUsername("ALICE"));
        Assert.Null(store.GetAccountByUsername("bob"));
        Assert.Null(store.GetAccountById(99));
    }

    [Fact]
    public void Seed_ThenGetCharacters_ReturnsInSlotOrder()
    {
        var store = new InMemoryAccountStore();
        var (account, chars) = MakeTestAccount(1, "alice", "First", "Second", "Third");
        store.SeedAccount(account, chars);

        var fetched = store.GetCharactersForAccount(1);
        Assert.Equal(3, fetched.Count);
        var fetchedNames = fetched.Select(c => c.Name).ToArray();
        var expectedNames = new[] { "First", "Second", "Third" };
        Assert.Equal(expectedNames, fetchedNames);
    }

    [Fact]
    public void GetCharactersForAccount_UnknownAccount_ReturnsEmpty()
    {
        var store = new InMemoryAccountStore();
        Assert.Empty(store.GetCharactersForAccount(999));
    }

    [Fact]
    public void Authenticate_CorrectPassword_ReturnsAccount()
    {
        var store = new InMemoryAccountStore();
        var (account, chars) = MakeTestAccount(1, "alice", "Char1");
        store.SeedAccount(account, chars);

        Assert.Equal(account, store.AuthenticateAccount("alice", "p1"));
    }

    [Fact]
    public void Authenticate_WrongPassword_ReturnsNull()
    {
        var store = new InMemoryAccountStore();
        var (account, chars) = MakeTestAccount(1, "alice", "Char1");
        store.SeedAccount(account, chars);

        Assert.Null(store.AuthenticateAccount("alice", "wrong"));
    }

    [Fact]
    public void Authenticate_UnknownUser_ReturnsNull()
    {
        var store = new InMemoryAccountStore();
        Assert.Null(store.AuthenticateAccount("nobody", "anything"));
    }

    [Fact]
    public void IssueSessionToken_AndValidate_ReturnsToken_ThenInvalidates()
    {
        var store = new InMemoryAccountStore();
        var (account, chars) = MakeTestAccount(42, "alice", "Char1");
        store.SeedAccount(account, chars);

        var token = store.IssueSessionToken(42, TimeSpan.FromMinutes(1));
        Assert.Equal(42u, token.AccountId);

        var validated = store.ValidateAndConsumeSessionToken(token.Value);
        Assert.NotNull(validated);
        Assert.Equal(42u, validated.AccountId);

        Assert.Null(store.ValidateAndConsumeSessionToken(token.Value));
    }

    [Fact]
    public void ValidateExpiredToken_ReturnsNull_AndStillRemoves()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var store = new InMemoryAccountStore(clock);
        var (account, chars) = MakeTestAccount(1, "alice", "Char1");
        store.SeedAccount(account, chars);

        var token = store.IssueSessionToken(1, TimeSpan.FromSeconds(10));
        clock.Advance(TimeSpan.FromMinutes(1));

        Assert.Null(store.ValidateAndConsumeSessionToken(token.Value));
        Assert.Null(store.ValidateAndConsumeSessionToken(token.Value));
    }

    [Fact]
    public void ValidateUnknownToken_ReturnsNull()
    {
        var store = new InMemoryAccountStore();
        Assert.Null(store.ValidateAndConsumeSessionToken(0xDEADBEEF));
    }

    [Fact]
    public void Seed_DuplicateAccountId_Throws()
    {
        var store = new InMemoryAccountStore();
        var (a1, c1) = MakeTestAccount(1, "alice", "Char1");
        store.SeedAccount(a1, c1);
        var (a2, c2) = MakeTestAccount(1, "bob", "Char2");
        Assert.Throws<InvalidOperationException>(() => store.SeedAccount(a2, c2));
    }

    [Fact]
    public void Seed_DuplicateUsername_Throws()
    {
        var store = new InMemoryAccountStore();
        var (a1, c1) = MakeTestAccount(1, "alice", "Char1");
        store.SeedAccount(a1, c1);
        var (a2, c2) = MakeTestAccount(2, "alice", "Char2");
        Assert.Throws<InvalidOperationException>(() => store.SeedAccount(a2, c2));
    }

    [Fact]
    public void Seed_CharacterOwnerMismatch_Throws()
    {
        var store = new InMemoryAccountStore();
        var account = new Account
        {
            AccountId = 1,
            Username = "alice",
            PasswordPlaceholder = "p",
            CharacterIds = new ulong[] { 100 },
        };
        var character = new Character
        {
            CharacterId = 100,
            OwnerAccountId = 999,
            Slot = 0,
            Name = "X",
            Species = Species.Buffalo,
            FType = FType.Power,
        };
        Assert.Throws<InvalidOperationException>(() =>
            store.SeedAccount(account, new[] { character }));
    }

    [Fact]
    public void DeleteCharacter_OwnedByCaller_RemovesCharacter()
    {
        var store = new InMemoryAccountStore();
        var (account, chars) = MakeTestAccount(1, "alice", "First", "Second", "Third");
        store.SeedAccount(account, chars);

        var deleted = store.DeleteCharacter(chars[1].CharacterId, ownerAccountId: 1);
        Assert.True(deleted);

        var remaining = store.GetCharactersForAccount(1);
        Assert.Equal(2, remaining.Count);
        Assert.DoesNotContain(remaining, c => c.CharacterId == chars[1].CharacterId);
    }

    [Fact]
    public void DeleteCharacter_WrongOwner_ReturnsFalseAndPreservesCharacter()
    {
        var store = new InMemoryAccountStore();
        var (acc1, chars1) = MakeTestAccount(1, "alice", "AliceChar");
        var (acc2, chars2) = MakeTestAccount(2, "bob", "BobChar");
        store.SeedAccount(acc1, chars1);
        store.SeedAccount(acc2, chars2);

        var deleted = store.DeleteCharacter(chars1[0].CharacterId, ownerAccountId: 2);
        Assert.False(deleted);
        Assert.Single(store.GetCharactersForAccount(1));
    }

    [Fact]
    public void DeleteCharacter_NotFound_ReturnsFalse()
    {
        var store = new InMemoryAccountStore();
        var (account, chars) = MakeTestAccount(1, "alice", "First");
        store.SeedAccount(account, chars);

        var deleted = store.DeleteCharacter(0xFFFFFFFFul, ownerAccountId: 1);
        Assert.False(deleted);
    }

    private sealed class FakeClock : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeClock(DateTimeOffset start) => _now = start;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan delta) => _now += delta;
    }
}
