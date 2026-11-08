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
    UpdatesNotTrusted,
}

public sealed class PlayFailure : HostingException
{
    public PlayFailure(PlayFailureReason reason, string summary, string? hint = null, Exception? inner = null)
        : base(summary, hint, inner)
    {
        Reason = reason;
    }

    public const string WebPortClosedHint =
        "If this is your server: open the web port (TCP 9990 by default) in your firewall and forward it on your router, and keep the web shop turned on, because the launcher gets its login pass from it. If it is someone else's server: tell its owner.";

    public PlayFailureReason Reason { get; }
}
