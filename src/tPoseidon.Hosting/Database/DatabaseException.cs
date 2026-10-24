namespace tPoseidon.Hosting.Database;

public enum DatabaseProblem
{
    Other,
    Unreachable,
    UnknownHost,
    TimedOut,
    WrongPassword,
    LoginRefused,
    UnknownDatabase,
    NotAllowed,
    TooOld,
}

public sealed class DatabaseException : HostingException
{
    public DatabaseException()
    {
    }

    public DatabaseException(string message)
        : base(message)
    {
    }

    public DatabaseException(string message, Exception inner)
        : base(message, inner)
    {
    }

    public DatabaseException(DatabaseProblem problem, string summary, string? hint, Exception? inner)
        : base(summary, hint, inner)
    {
        Problem = problem;
    }

    public DatabaseProblem Problem { get; }
}
