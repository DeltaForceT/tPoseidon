namespace tPoseidon.Core.Game.PvP;

public sealed class PvpDuelRequests
{
    private readonly Dictionary<ulong, ulong> _asked = new();
    private readonly HashSet<(ulong Requester, ulong Target)> _accepted = new();

    public void Ask(ulong requester, ulong target)
    {
        Forget(requester);
        _asked[requester] = target;
    }

    public bool Answer(ulong responder, ulong requester, bool accepted)
    {
        if (!_asked.TryGetValue(requester, out var target) || target != responder) return false;
        _asked.Remove(requester);
        if (accepted) _accepted.Add((requester, responder));
        return true;
    }

    public bool Start(ulong requester, ulong target) => _accepted.Remove((requester, target));

    public void Cancel(ulong requester, ulong target)
    {
        if (_asked.TryGetValue(requester, out var asked) && asked == target) _asked.Remove(requester);
        _accepted.Remove((requester, target));
    }

    public void Forget(ulong character)
    {
        _asked.Remove(character);
        foreach (var requester in _asked.Where(pair => pair.Value == character).Select(pair => pair.Key).ToArray()) _asked.Remove(requester);
        _accepted.RemoveWhere(pair => pair.Requester == character || pair.Target == character);
    }
}
