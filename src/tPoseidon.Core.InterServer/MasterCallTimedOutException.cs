namespace tPoseidon.Core.InterServer;

public sealed class MasterCallTimedOutException : Exception
{
    public MasterCallTimedOutException(string operation, string subject, Exception inner)
        : base($"master did not answer {operation} for {subject} in time; the write may or may not have been applied", inner)
    {
        Operation = operation;
        Subject = subject;
    }

    public string Operation { get; }

    public string Subject { get; }
}
