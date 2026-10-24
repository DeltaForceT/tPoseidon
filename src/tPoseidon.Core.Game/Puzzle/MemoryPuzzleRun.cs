using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Puzzle;

public enum MemoryPuzzlePhase
{
    Showing,
    Input,
    Finished,
}

public static class PuzzleRules
{
    public const uint Solved = 0;
    public const uint Failed = 1;
}

public sealed class MemoryPuzzleRun
{
    private readonly List<(int PositionId, int MonsterId)> _order;
    private readonly List<ushort> _spawned = new();
    private DateTimeOffset _nextShow;
    private int _killed;

    public MemoryPuzzleRun(MemoryPuzzleEntry puzzle, ushort mapId, IReadOnlyList<(int PositionId, int MonsterId)> order, DateTimeOffset now)
    {
        Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
        ArgumentNullException.ThrowIfNull(order);
        MapId = mapId;
        _order = order.ToList();
        _nextShow = now;
    }

    public MemoryPuzzleEntry Puzzle { get; }
    public ushort MapId { get; }
    public MemoryPuzzlePhase Phase { get; private set; } = MemoryPuzzlePhase.Showing;
    public DateTimeOffset InputStarted { get; private set; }
    public DateTimeOffset Deadline { get; private set; }
    public uint? Result { get; private set; }
    public IReadOnlyList<ushort> Spawned => _spawned;

    public static IReadOnlyList<(int PositionId, int MonsterId)> ChooseOrder(
        IReadOnlyList<(int PositionId, int MonsterId)> objects, IReadOnlySet<int> mapPoints, int count, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(mapPoints);
        ArgumentNullException.ThrowIfNull(next);
        var pool = objects.Where(entry => mapPoints.Contains(entry.PositionId)).DistinctBy(entry => entry.PositionId).ToList();
        var order = new List<(int, int)>();
        while (order.Count < count && pool.Count > 0)
        {
            var index = next(pool.Count);
            order.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return order;
    }

    public (int PositionId, int MonsterId)? NextToShow(DateTimeOffset now)
    {
        if (Phase != MemoryPuzzlePhase.Showing || _spawned.Count >= _order.Count || now < _nextShow) return null;
        _nextShow = now.AddSeconds(Math.Max(0, Puzzle.TurnSeconds));
        return _order[_spawned.Count];
    }

    public void Shown(ushort entityId) => _spawned.Add(entityId);

    public bool StartInputIfShown(DateTimeOffset now)
    {
        if (Phase != MemoryPuzzlePhase.Showing || _spawned.Count < _order.Count || now < _nextShow) return false;
        Phase = MemoryPuzzlePhase.Input;
        InputStarted = now;
        Deadline = now.AddSeconds(Puzzle.TimeLimitSeconds);
        return true;
    }

    public bool Killed(ushort entityId)
    {
        var index = _spawned.IndexOf(entityId);
        if (index < 0 || Phase == MemoryPuzzlePhase.Finished) return false;
        if (Phase != MemoryPuzzlePhase.Input || index != _killed)
        {
            Finish(PuzzleRules.Failed);
            return true;
        }
        _killed++;
        if (_killed == _spawned.Count) Finish(PuzzleRules.Solved);
        return true;
    }

    public bool TimedOut(DateTimeOffset now)
    {
        if (Phase != MemoryPuzzlePhase.Input || now < Deadline) return false;
        Finish(PuzzleRules.Failed);
        return true;
    }

    public uint RecordSeconds(DateTimeOffset now) => Result == PuzzleRules.Solved ? (uint)Math.Max(0, (now - InputStarted).TotalSeconds) : 0;

    private void Finish(uint result)
    {
        Result = result;
        Phase = MemoryPuzzlePhase.Finished;
    }
}
