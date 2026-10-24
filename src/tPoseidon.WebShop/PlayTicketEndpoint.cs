using Microsoft.AspNetCore.Http;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Security;

namespace tPoseidon.WebShop;

public sealed class PlayTicketEndpoint
{
    public const string UsernameField = "username";
    public const string PasswordField = "password";

    private readonly IAccountStore _accounts;
    private readonly ILaunchTicketStore _tickets;
    private readonly LoginThrottle? _throttle;

    public PlayTicketEndpoint(IAccountStore accounts, ILaunchTicketStore tickets, LoginThrottle? throttle)
    {
        _accounts = accounts ?? throw new ArgumentNullException(nameof(accounts));
        _tickets = tickets ?? throw new ArgumentNullException(nameof(tickets));
        _throttle = throttle;
    }

    public async Task HandleAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!context.Request.HasFormContentType)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted).ConfigureAwait(false);
        var username = form[UsernameField].ToString().Trim();
        var password = form[PasswordField].ToString();
        if (username.Length == 0 || password.Length == 0)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var remote = LaunchTicket.Normalize(context.Connection.RemoteIpAddress ?? System.Net.IPAddress.None);
        if (_throttle?.Check(remote, username).LockedOut == true)
        {
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            return;
        }

        var account = _accounts.VerifyPassword(username, password) ? _accounts.GetAccountByUsername(username) : null;
        if (account is null)
        {
            if (_throttle?.Failed(remote, username) is { } failure)
            {
                await Task.Delay(failure.Delay, context.RequestAborted).ConfigureAwait(false);
            }

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        _throttle?.Succeeded(username);
        _tickets.Issue(account.AccountId, [remote], LaunchTicket.Lifetime);
        context.Response.StatusCode = StatusCodes.Status200OK;
    }
}
