using System.Net;
using tPoseidon.Core.Data;

namespace tPoseidon.WebShop;

public enum ShopRefusal
{
    None,
    UnknownAccount,
    NotInGame,
    OtherAddress,
}

public static class ShopGate
{
    public static ShopRefusal Check(ShopAccount? account, IPAddress remoteIp)
    {
        ArgumentNullException.ThrowIfNull(remoteIp);
        if (account is null) return ShopRefusal.UnknownAccount;
        if (account.GameState != ShopAccountRules.InGameState) return ShopRefusal.NotInGame;
        if (account.LatestIp is null || !Normalize(account.LatestIp).Equals(Normalize(remoteIp))) return ShopRefusal.OtherAddress;
        return ShopRefusal.None;
    }

    public static IPAddress Normalize(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return IPAddress.IsLoopback(address) ? IPAddress.Loopback : address;
    }
}
