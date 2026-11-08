namespace tPoseidon.Core.Game.Characters;

public sealed class DisguiseHandoff
{
    private readonly object _gate = new();
    private readonly Dictionary<ulong, (DateTimeOffset At, int ShownType)> _held = new();

    public void Store(ulong characterId, DisguiseState state, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            if (state.Disguised && !state.WearsDress) _held[characterId] = (now, state.ShownType);
            else _held.Remove(characterId);
        }
    }

    public int? Take(ulong characterId, DateTimeOffset now, TimeSpan timeToLive)
    {
        (DateTimeOffset At, int ShownType) held;
        lock (_gate)
        {
            if (!_held.Remove(characterId, out held)) return null;
        }
        return now - held.At > timeToLive ? null : held.ShownType;
    }
}
