using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using Xunit;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class LoginSessionHandlerTests
{
    private const uint TestAccountId = 1;
    private const ulong TestCharId = 0xCAFEBABEul;
    private const uint TestToken = 0x12345678;

    private static InMemoryAccountStore MakeStoreWithDevSeed()
    {
        var store = new InMemoryAccountStore();
        store.SeedAccount(
            new Account
            {
                AccountId = TestAccountId,
                Username = "test",
                PasswordPlaceholder = "test",
                CharacterIds = new[] { TestCharId },
            },
            new[]
            {
                new Character
                {
                    CharacterId = TestCharId,
                    OwnerAccountId = TestAccountId,
                    Slot = 0,
                    Name = "TestCharacter",
                    Species = Species.Buffalo,
                    FType = FType.Power,
                },
            });
        store.SeedSessionToken(TestToken, TestAccountId, TimeSpan.FromMinutes(5));
        return store;
    }

    private static byte[] MakeSessionInitPayload(uint charId, uint token) =>
        new SessionInitPacket
        {
            CharacterId = charId,
            Reserved4 = 0,
            SessionToken = token,
        }.EncodePayload();

    [Fact]
    public void Handle_ValidTokenAndCharacter_ReturnsAccept()
    {
        var store = MakeStoreWithDevSeed();
        var handler = new LoginSessionHandler(store);
        var payload = MakeSessionInitPayload((uint)TestCharId, TestToken);

        var result = handler.Handle(payload);

        Assert.True(result.Accepted);
        Assert.Equal(TestAccountId, result.AccountId);
        Assert.Equal("TestCharacter", result.Character!.Name);
        Assert.Null(result.RejectReason);
    }

    [Fact]
    public void Handle_TokenIsConsumed_OnSuccess()
    {
        var store = MakeStoreWithDevSeed();
        var handler = new LoginSessionHandler(store);
        var payload = MakeSessionInitPayload((uint)TestCharId, TestToken);

        var first = handler.Handle(payload);
        Assert.True(first.Accepted);

        var second = handler.Handle(payload);
        Assert.False(second.Accepted);
        Assert.Equal(LoginRejectReason.InvalidToken, second.RejectReason);
    }

    [Fact]
    public void Handle_UnknownToken_RejectsInvalidToken()
    {
        var store = MakeStoreWithDevSeed();
        var handler = new LoginSessionHandler(store);
        var payload = MakeSessionInitPayload((uint)TestCharId, 0xBADBADBA);

        var result = handler.Handle(payload);
        Assert.False(result.Accepted);
        Assert.Equal(LoginRejectReason.InvalidToken, result.RejectReason);
    }

    [Fact]
    public void Handle_UnknownCharacter_RejectsUnknownCharacter()
    {
        var store = MakeStoreWithDevSeed();
        var handler = new LoginSessionHandler(store);
        var payload = MakeSessionInitPayload(0xDEADDEAD, TestToken);

        var result = handler.Handle(payload);
        Assert.False(result.Accepted);
        Assert.Equal(LoginRejectReason.UnknownCharacter, result.RejectReason);
    }

    [Fact]
    public void Handle_CharacterOwnedByDifferentAccount_RejectsOwnershipMismatch()
    {
        var store = MakeStoreWithDevSeed();
        const uint OtherAccountId = 2;
        const ulong OtherCharId = 0xFEEDFACEul;
        store.SeedAccount(
            new Account
            {
                AccountId = OtherAccountId,
                Username = "other",
                PasswordPlaceholder = "other",
                CharacterIds = new[] { OtherCharId },
            },
            new[]
            {
                new Character
                {
                    CharacterId = OtherCharId,
                    OwnerAccountId = OtherAccountId,
                    Slot = 0,
                    Name = "OtherCharacter",
                    Species = Species.Buffalo,
                    FType = FType.Power,
                },
            });

        var handler = new LoginSessionHandler(store);
        var payload = MakeSessionInitPayload((uint)OtherCharId, TestToken);

        var result = handler.Handle(payload);
        Assert.False(result.Accepted);
        Assert.Equal(LoginRejectReason.CharacterOwnershipMismatch, result.RejectReason);
    }

    [Fact]
    public void Handle_MalformedPayload_RejectsMalformedSessionInit()
    {
        var store = MakeStoreWithDevSeed();
        var handler = new LoginSessionHandler(store);

        var result = handler.Handle(new byte[5]);
        Assert.False(result.Accepted);
        Assert.Equal(LoginRejectReason.MalformedSessionInit, result.RejectReason);
    }

    [Fact]
    public void Handle_OwnershipFailure_DoesNotConsumeToken()
    {
        var store = MakeStoreWithDevSeed();
        var handler = new LoginSessionHandler(store);
        var badPayload = MakeSessionInitPayload(0xDEADDEAD, TestToken);

        var first = handler.Handle(badPayload);
        Assert.False(first.Accepted);
        Assert.Equal(LoginRejectReason.UnknownCharacter, first.RejectReason);

        var goodPayload = MakeSessionInitPayload((uint)TestCharId, TestToken);
        var second = handler.Handle(goodPayload);
        Assert.False(second.Accepted);
        Assert.Equal(LoginRejectReason.InvalidToken, second.RejectReason);
    }
}
