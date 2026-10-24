namespace tPoseidon.Cli.Terminal;

internal sealed class ConsoleProgress<T> : IProgress<T>
{
    private readonly Action<T> _write;

    public ConsoleProgress(Action<T> write)
    {
        _write = write;
    }

    public void Report(T value) => _write(value);
}
