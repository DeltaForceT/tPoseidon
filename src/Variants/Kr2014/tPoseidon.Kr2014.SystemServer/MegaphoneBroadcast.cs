namespace tPoseidon.Kr2014.SystemServer;

public static class MegaphoneBroadcast
{
    public const int SendAttempts = 3;

    public static async Task<int> SendWithRetryAsync(Func<CancellationToken, Task> send, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(send);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await send(ct).ConfigureAwait(false);
                return attempt;
            }
            catch (Exception ex) when (attempt < SendAttempts && ex is ObjectDisposedException or IOException or System.Net.Sockets.SocketException)
            {
            }
        }
    }
}
