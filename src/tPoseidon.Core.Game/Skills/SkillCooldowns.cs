namespace tPoseidon.Core.Game.Skills;

public sealed class SkillCooldowns
{
    public const float StartedEarlierBy = 0.5f;

    private readonly Dictionary<int, (int Elapsed, int Total)> _running = new();

    public bool IsRunning(int skillId) => _running.ContainsKey(skillId);

    public void Start(int skillId, float coolTimeSeconds)
    {
        var seconds = coolTimeSeconds - StartedEarlierBy;
        if (seconds <= 0f || _running.ContainsKey(skillId)) return;
        _running[skillId] = (0, (int)((double)seconds * 1000.0));
    }

    public void Stop(int skillId) => _running.Remove(skillId);

    public void Tick(int elapsedMs)
    {
        if (_running.Count == 0) return;
        List<int>? over = null;
        foreach (var (skillId, (elapsed, total)) in _running)
        {
            if (elapsed < total) continue;
            (over ??= new List<int>()).Add(skillId);
        }
        foreach (var skillId in _running.Keys.ToArray())
        {
            var (elapsed, total) = _running[skillId];
            if (elapsed < total) _running[skillId] = (elapsed + elapsedMs, total);
        }
        if (over is null) return;
        foreach (var skillId in over) _running.Remove(skillId);
    }
}
