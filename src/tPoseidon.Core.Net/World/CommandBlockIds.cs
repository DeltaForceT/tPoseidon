namespace tPoseidon.Core.Net.World;

public sealed class CommandBlockIds
{
    private readonly object _lock = new();
    private ushort _current = MeleeSwingFrames.NoBlock;

    public ushort Next()
    {
        lock (_lock)
        {
            _current = MeleeSwingFrames.NextBlockId(_current);
            return _current;
        }
    }
}
