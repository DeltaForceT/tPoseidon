using System.Data.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.DrillEgg.Connectors;

internal sealed record GachaPlayer(string Account, long? AccountId, string? Character);

internal enum SignInKind
{
    Automatic,
    Name,
    Password,
    Token,
}

internal sealed record SignInRequest(string? Account, string? Password, string? Token);

internal sealed record Delivery(long DrawId, GachaPlayer Player, GachaPrize Prize, string ItemName, short Area, bool Deep);

internal sealed class ConnectorException : Exception
{
    public ConnectorException(string message)
        : base(message)
    {
    }

    public ConnectorException(string message, Exception inner)
        : base(message, inner)
    {
    }

    public ConnectorException()
    {
    }
}

internal sealed record SqlUnit(SqlDatabase Database, DbConnection Connection, DbTransaction Transaction);

internal interface IPlayerConnector
{
    SignInKind Kind { get; }

    Task<GachaPlayer?> SignInAsync(SignInRequest request, CancellationToken ct);
}

internal interface ICouponConnector
{
    Task<int> CountAsync(GachaPlayer player, CancellationToken ct);

    Task<ICouponSpend?> SpendAsync(GachaPlayer player, int cost, long drawId, CancellationToken ct);
}

internal interface ICouponSpend : IAsyncDisposable
{
    int CouponsLeft { get; }

    SqlUnit? Unit { get; }

    Task CommitAsync(CancellationToken ct);
}

internal interface IDeliveryConnector
{
    SqlDatabase? Database { get; }

    Task DeliverAsync(Delivery delivery, SqlUnit? joined, CancellationToken ct);
}
