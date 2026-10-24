using System.Net;

namespace tPoseidon.Core.Data;

public sealed record ShopAccount(uint AccountId, string Username, int GameState, IPAddress? LatestIp);

public static class ShopAccountRules
{
    public const int InGameState = 4;
}

public interface IShopAccountStore
{
    ShopAccount? Find(string username);

    ShopAccount? Find(uint accountId);

    IReadOnlyList<ShopAccount> InGameAt(IPAddress address);
}
