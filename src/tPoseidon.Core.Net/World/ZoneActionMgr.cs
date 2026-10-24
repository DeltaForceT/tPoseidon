using tPoseidon.Core.Game.Combat;

namespace tPoseidon.Core.Net.World;

public sealed class ZoneActionMgr
{
    private readonly Zone _zone;
    private readonly ActionManager _global;

    public ZoneActionMgr(Zone zone, ActionManager global)
    {
        _zone = zone ?? throw new ArgumentNullException(nameof(zone));
        _global = global ?? throw new ArgumentNullException(nameof(global));
    }

    public Zone Zone => _zone;
    public ActionManager Global => _global;

    public bool IsInitialized { get; private set; }

    public void Init()
    {
        IsInitialized = true;
    }
}
