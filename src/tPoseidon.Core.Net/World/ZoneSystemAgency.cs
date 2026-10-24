namespace tPoseidon.Core.Net.World;

public sealed class ZoneSystemAgency
{
    private readonly Zone _zone;

    public ZoneSystemAgency(Zone zone)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
    }

    public Zone Zone => _zone;

    public bool IsInitialized { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }
}
