namespace tPoseidon.Gacha.Art.Swf;

public sealed class SwfFormatException : Exception
{
    public SwfFormatException()
    {
    }

    public SwfFormatException(string message)
        : base(message)
    {
    }

    public SwfFormatException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
