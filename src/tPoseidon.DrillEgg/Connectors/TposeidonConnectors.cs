using System.Data.Common;
using System.Globalization;
using tPoseidon.Core.Data;

namespace tPoseidon.DrillEgg.Connectors;

internal sealed class TposeidonPlayers : IPlayerConnector
{
    private readonly IAccountStore _accounts;

    public TposeidonPlayers(IAccountStore accounts)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
    }

    public SignInKind Kind => SignInKind.Password;

    public Task<GachaPlayer?> SignInAsync(SignInRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.Account?.Trim();
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(request.Password)) return Task.FromResult<GachaPlayer?>(null);
        try
        {
            if (!_accounts.VerifyPassword(name, request.Password)) return Task.FromResult<GachaPlayer?>(null);
            var account = _accounts.GetAccountByUsername(name);
            return Task.FromResult(account is null ? null : new GachaPlayer(account.Username, account.AccountId, null));
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The tPoseidon account check failed: {e.Message}", e);
        }
    }
}

internal sealed class TposeidonDelivery : IDeliveryConnector
{
    private readonly IChargedItemStore _items;

    public TposeidonDelivery(IChargedItemStore items)
    {
        _items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public SqlDatabase? Database => null;

    public Task DeliverAsync(Delivery delivery, SqlUnit? joined, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        if (delivery.Player.AccountId is not { } id || id is < 0 or > uint.MaxValue)
        {
            throw new ConnectorException($"Draw {delivery.DrawId.ToString(CultureInfo.InvariantCulture)} cannot go to the tPoseidon item box: player {delivery.Player.Account} has no emulator account id.");
        }
        var prize = delivery.Prize;
        try
        {
            _items.Grant(new ChargedItemGrant(
                (uint)id, prize.ItemClass, prize.ItemIndex, prize.Count, prize.UseTimeHours, prize.ItemFlag,
                checked((int)(delivery.DrawId + ChargedItemRules.GachaSellCodeBase)), ChargedItemRules.PurchaseSellType));
            return Task.CompletedTask;
        }
        catch (DbException e)
        {
            throw new ConnectorException($"The tPoseidon item box insert failed for draw {delivery.DrawId.ToString(CultureInfo.InvariantCulture)}: {e.Message}", e);
        }
    }
}
