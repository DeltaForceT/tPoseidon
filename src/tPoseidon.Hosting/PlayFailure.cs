namespace tPoseidon.Hosting;

public enum PlayFailureReason
{
    WrongPassword,
    TooManyAttempts,
    ServerUnreachable,
    WebPortClosed,
    ServerRefused,
    NotKr2014Client,
    ClientNotWritable,
    ClientRunning,
    ServerBusy,
    UpdateFailed,
}

public sealed class PlayFailure : HostingException
{
    public PlayFailure(PlayFailureReason reason, string summary, string? hint = null, Exception? inner = null)
        : base(summary, hint, inner)
    {
        Reason = reason;
    }

    public PlayFailureReason Reason { get; }
}
