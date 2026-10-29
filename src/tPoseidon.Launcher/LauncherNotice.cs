using tPoseidon.Hosting;
using tPoseidon.Ui;

namespace tPoseidon.Launcher;

public sealed record LauncherNotice(BannerKind Kind, string Title, string? Detail)
{
    public static LauncherNotice Error(string title, string? detail = null) => new(BannerKind.Error, title, detail);

    public static LauncherNotice From(HostingException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is not PlayFailure failure)
        {
            return Error(exception.Summary, exception.Hint);
        }

        var detail = exception.Hint is null ? exception.Summary : $"{exception.Summary} {exception.Hint}";
        return failure.Reason switch
        {
            PlayFailureReason.WrongPassword => Error("Wrong account name or password", "Check both and try again."),
            PlayFailureReason.TooManyAttempts => Error("Too many failed logins", "Wait a few minutes and try again."),
            PlayFailureReason.ServerUnreachable => Error("The server cannot be reached", detail),
            PlayFailureReason.WebPortClosed => Error("The server's web port is closed", detail),
            PlayFailureReason.ServerRefused => Error("The server refused the request", detail),
            PlayFailureReason.NotKr2014Client => Error("This is not the KR 2014 client", detail),
            PlayFailureReason.ClientNotWritable => Error("The client folder cannot be changed", detail),
            PlayFailureReason.ServerBusy => Error("The server is busy", "Too many requests from your connection. Wait a moment and try again."),
            PlayFailureReason.UpdateFailed => Error("The update could not be applied", detail),
            PlayFailureReason.ClientRunning => Error("The game client is already running", "Close it, then press Play again."),
            _ => throw new ArgumentOutOfRangeException(nameof(exception), failure.Reason, "Unknown play failure."),
        };
    }
}
