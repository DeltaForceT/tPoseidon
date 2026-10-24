using tPoseidon.Hosting;

namespace tPoseidon.Cli;

internal sealed class CliException : HostingException
{
    public CliException(string message)
        : base(message)
    {
    }

    public CliException(string message, Exception inner)
        : base(message, inner)
    {
    }

    public CliException()
    {
    }
}
