namespace tPoseidon.Core.Net.World;

public sealed class ZoneTrapMgr
{
    private readonly Zone _zone;

    public ZoneTrapMgr(Zone zone)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    public bool IsInitialized { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }
}
