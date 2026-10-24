namespace tPoseidon.Hosting;

public class HostingException : Exception
{
    public HostingException()
    {
        Summary = Message;
    }

    public HostingException(string message)
        : base(message)
    {
        Summary = message;
    }

    public HostingException(string message, Exception inner)
        : base(message, inner)
    {
        Summary = message;
    }

    public HostingException(string summary, string? hint, Exception? inner)
        : base(hint is null ? summary : summary + Environment.NewLine + hint, inner)
    {
        Summary = summary;
        Hint = hint;
    }

    public string Summary { get; }

    public string? Hint { get; }
}
