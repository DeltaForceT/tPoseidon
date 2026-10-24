using tPoseidon.Core.Data;
using tPoseidon.Core.Net.S2;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class LobbyLoginTests
{
    [Theory]
    [InlineData(0, LobbyLoginRefusal.None)]
    [InlineData(1, LobbyLoginRefusal.AccountBlocked)]
    [InlineData(2, LobbyLoginRefusal.AccountWithdrawn)]
    public void AccountAccessDecidesTheRefusal(short access, LobbyLoginRefusal expected)
    {
        Assert.Equal(expected, LobbyLogin.ForAccess(access));
    }

    [Fact]
    public void TheRefusalIsTheCodeAsALittleEndianU32()
    {
        Assert.Equal(new byte[] { 0x63, 0xEA, 0x00, 0x00 }, LobbyLogin.RefusalPayload(LobbyLoginRefusal.WrongIdOrPassword));
        Assert.Equal(60009u, (uint)LobbyLoginRefusal.AccountBlocked);
        Assert.Equal(0x2CEF, LobbyLogin.RefusalOpcode);
    }

    [Fact]
    public void TheInMemoryStoreKeepsTheAccountAccess()
    {
        var store = new InMemoryAccountStore();

        Assert.Equal(0, store.AccountAccess(5));
        store.SetAccountAccess(5, LobbyLogin.BlockedAccess);
        Assert.Equal(LobbyLoginRefusal.AccountBlocked, LobbyLogin.ForAccess(store.AccountAccess(5)));
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly PenaltyNotice Penalty = new("Abuse", new DateTimeOffset(2026, 11, 2, 9, 30, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(0, null, LobbyLoginRefusal.None)]
    [InlineData(2, null, LobbyLoginRefusal.AlreadyLoggedIn)]
    [InlineData(4, null, LobbyLoginRefusal.AlreadyLoggedIn)]
    [InlineData(3, 10, LobbyLoginRefusal.AlreadyLoggedIn)]
    [InlineData(3, 31, LobbyLoginRefusal.None)]
    public void AnAccountStillMarkedLoggedInIsRefusedLikeTheKrLobby(short state, int? secondsSinceLogin, LobbyLoginRefusal expected)
    {
        var account = new LobbyAccountState(0, 0, state, secondsSinceLogin is { } seconds ? Now.AddSeconds(-seconds) : null);

        Assert.Equal(expected, LobbyLogin.Decide(account, null, Now));
    }

    [Fact]
    public void AGameBlockNeedsARunningPenaltyRow()
    {
        Assert.Equal(LobbyLoginRefusal.GameBlocked, LobbyLogin.Decide(new LobbyAccountState(0, 1, 0, null), Penalty, Now));
        Assert.Equal(LobbyLoginRefusal.None, LobbyLogin.Decide(new LobbyAccountState(0, 1, 0, null), null, Now));
        Assert.Equal(LobbyLoginRefusal.AccountBlocked, LobbyLogin.Decide(new LobbyAccountState(1, 1, 4, null), Penalty, Now));
        Assert.Equal(LobbyLoginRefusal.GameBlocked, LobbyLogin.Decide(new LobbyAccountState(0, 1, 4, null), Penalty, Now));
        Assert.Equal(60012u, (uint)LobbyLoginRefusal.GameBlocked);
        Assert.Equal(60013u, (uint)LobbyLoginRefusal.AlreadyLoggedIn);
    }

    [Fact]
    public void TheBlockNoticeIsANameOf256BytesAndTheReleaseSystemTime()
    {
        var payload = LobbyLogin.BlockNoticePayload(Penalty, TimeZoneInfo.Utc, System.Text.Encoding.ASCII);

        Assert.Equal(0x110, payload.Length);
        Assert.Equal("Abuse"u8.ToArray(), payload[..5]);
        Assert.All(payload[5..256], value => Assert.Equal(0, value));
        Assert.Equal(new byte[] { 0xEA, 0x07, 0x0B, 0x00, 0x00, 0x00, 0x02, 0x00, 0x09, 0x00, 0x1E, 0x00, 0x00, 0x00, 0x00, 0x00 }, payload[256..]);
        Assert.Equal(0x2CF4, LobbyLogin.BlockNoticeOpcode);
    }

    [Fact]
    public void TheInMemoryStoreFollowsTheKrStateSteps()
    {
        var store = new InMemoryAccountStore();
        var account = store.RegisterAccount("player", "hash");
        store.IssueGameAuthcode(account.AccountId, 77, System.Net.IPAddress.Loopback);

        Assert.True(store.TransitionToServerSelected(account.AccountId, 1, 1));
        Assert.False(store.JoinGame(account.AccountId, 78, 1));
        Assert.True(store.JoinGame(account.AccountId, 77, 1));
        Assert.Equal(4, store.GetLobbyAccountState(account.AccountId)!.State);
        store.TransitionToWorldSelected(account.AccountId, System.Net.IPAddress.Loopback, 1, 1, 0x9001);
        Assert.Equal(0x9001ul, store.FindPendingGameJoin(System.Net.IPAddress.Loopback, TimeSpan.FromSeconds(30))!.SelectedCharacterId);
        store.TransitionToInGame(account.AccountId, 1);
        Assert.Null(store.FindPendingGameJoin(System.Net.IPAddress.Loopback, TimeSpan.FromSeconds(30)));
        Assert.Equal(1, store.ResetWorldStates());
        Assert.Equal(0, store.GetLobbyAccountState(account.AccountId)!.State);
    }

    [Fact]
    public void TheLobbyResetsOnlyLobbyAndServerSelectStates()
    {
        var store = new InMemoryAccountStore();
        var lobby = store.RegisterAccount("lobby", "hash");
        var game = store.RegisterAccount("game", "hash");
        store.IssueGameAuthcode(lobby.AccountId, 1, System.Net.IPAddress.Loopback);
        store.IssueGameAuthcode(game.AccountId, 2, System.Net.IPAddress.Loopback);
        store.TransitionToServerSelected(game.AccountId, 1, 1);
        store.JoinGame(game.AccountId, 2, 1);

        Assert.True(store.EndLobbySession(lobby.AccountId));
        Assert.False(store.EndLobbySession(game.AccountId));
        store.IssueGameAuthcode(lobby.AccountId, 3, System.Net.IPAddress.Loopback);
        Assert.Equal(1, store.ResetLobbyStates());
        Assert.Equal(4, store.GetLobbyAccountState(game.AccountId)!.State);
    }

    [Fact]
    public void AnEndedPenaltyIsReleased()
    {
        var clock = new tPoseidon.Core.Net.Tests.Security.ManualTime();
        var store = new InMemoryAccountStore(clock);
        var account = store.RegisterAccount("player", "hash");
        store.SetGameAccess(account.AccountId, LobbyLogin.BlockedGameAccess);
        store.SetRunningPenalty(account.AccountId, Penalty with { ReleaseAt = clock.GetUtcNow().AddHours(1) });

        Assert.Equal(0, store.ReleaseEndedPenalties());
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Equal(1, store.ReleaseEndedPenalties());
        Assert.Null(store.FindRunningPenalty(account.AccountId));
        Assert.Equal(0, store.GetLobbyAccountState(account.AccountId)!.GameAccess);
    }
}
