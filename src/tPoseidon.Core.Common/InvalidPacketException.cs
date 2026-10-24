namespace tPoseidon.Core.Common;

public sealed class InvalidPacketException : Exception
{
    public InvalidPacketException(string message) : base(message) { }

    public InvalidPacketException(string message, Exception inner) : base(message, inner) { }
}
