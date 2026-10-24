namespace tPoseidon.Core.Common;

public sealed class MemTracker
{
    private long _lastBytes;
    private readonly Action<string> _log;

    public MemTracker(Action<string> log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _lastBytes = GC.GetTotalMemory(forceFullCollection: false);
    }

    public void Mark(string label)
    {
        var current = GC.GetTotalMemory(forceFullCollection: false);
        var deltaBytes = current - _lastBytes;
        var currentMb = current / 1024.0 / 1024.0;
        var deltaMb = deltaBytes / 1024.0 / 1024.0;
        var sign = deltaMb >= 0 ? "+" : "";
        _log($"<MEM> RAM={currentMb:F1} MB ({sign}{deltaMb:F2} MB) {label}");
        _lastBytes = current;
    }
}
