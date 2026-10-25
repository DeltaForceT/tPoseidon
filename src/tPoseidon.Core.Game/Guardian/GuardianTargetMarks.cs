namespace tPoseidon.Core.Game.Guardian;

public sealed class GuardianTargetMarks
{
    private readonly List<ushort> _targets = [];

    public bool Enabled { get; private set; }

    public IReadOnlyList<ushort> Targets => _targets;

    public ushort? First => _targets.Count > 0 ? _targets[0] : null;

    public bool Mark(ushort entityId)
    {
        if (_targets.Contains(entityId)) return false;
        _targets.Clear();
        _targets.Add(entityId);
        Enabled = true;
        return true;
    }

    public bool Release(ushort entityId)
    {
        if (!_targets.Remove(entityId)) return false;
        if (_targets.Count == 0) Enabled = false;
        return true;
    }

    public bool Clear()
    {
        var had = _targets.Count > 0 || Enabled;
        _targets.Clear();
        Enabled = false;
        return had;
    }
}
