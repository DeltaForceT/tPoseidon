using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Puzzle;

public readonly record struct SlideMove(int From, int To);

public static class SlidePuzzleBoard
{
    public const int ColumnStepX = 80;
    public const int ColumnStepY = 40;
    public const int ShuffleMovesPerTile = 100;

    public static (int X, int Y) Origin(int centreX, int centreY, int columns, int rows) =>
        (centreX - (columns + rows) * ColumnStepX / 2, centreY - (columns + rows) * ColumnStepY / 2);

    public static (int X, int Y) TileCentre(int originX, int originY, int columns, int rows, int index)
    {
        var row = index / columns;
        var column = index % columns;
        return (originX + (rows - row + column) * ColumnStepX, originY + (row + column) * ColumnStepY + ColumnStepY);
    }

    public static int? TileAt(int originX, int originY, int columns, int rows, int x, int y)
    {
        var across = (x - originX) / (double)ColumnStepX;
        var down = (y - originY - ColumnStepY) / (double)ColumnStepY;
        var column = (int)Math.Floor((across + down - rows) / 2 + 0.5);
        var row = (int)Math.Floor((down - across + rows) / 2 + 0.5);
        if (column < 0 || column >= columns || row < 0 || row >= rows) return null;
        return row * columns + column;
    }

    public static bool Adjacent(int a, int b, int columns)
    {
        var (rowA, columnA) = (a / columns, a % columns);
        var (rowB, columnB) = (b / columns, b % columns);
        return Math.Abs(rowA - rowB) + Math.Abs(columnA - columnB) == 1;
    }

    public static IEnumerable<int> Neighbours(int index, int columns, int rows)
    {
        var row = index / columns;
        var column = index % columns;
        if (row > 0) yield return index - columns;
        if (row < rows - 1) yield return index + columns;
        if (column > 0) yield return index - 1;
        if (column < columns - 1) yield return index + 1;
    }

    public static int[] Shuffle(IReadOnlyList<SlidePuzzleBlock> blocks, int columns, int rows, Func<int, int> next)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(next);
        var solved = blocks.Select(block => block.Id).ToArray();
        var emptyIndex = Array.FindIndex(blocks.ToArray(), block => block.IsEmpty);
        if (solved.Length != columns * rows || solved.Length < 2 || emptyIndex < 0)
        {
            throw new ArgumentException($"a {columns} x {rows} board needs {columns * rows} blocks with one empty block", nameof(blocks));
        }
        while (true)
        {
            var grid = (int[])solved.Clone();
            var hole = emptyIndex;
            var previous = -1;
            for (var move = 0; move < ShuffleMovesPerTile * solved.Length; move++)
            {
                var choices = Neighbours(hole, columns, rows).Where(index => index != previous).ToArray();
                if (choices.Length == 0) choices = Neighbours(hole, columns, rows).ToArray();
                var chosen = choices[next(choices.Length)];
                (grid[hole], grid[chosen]) = (grid[chosen], grid[hole]);
                previous = hole;
                hole = chosen;
            }
            if (!grid.SequenceEqual(solved)) return grid;
        }
    }
}

public sealed class SlidePuzzleRun
{
    private readonly int[] _grid;
    private readonly int[] _solved;
    private int? _standingOn;

    public SlidePuzzleRun(
        SlidePuzzleEntry puzzle, ushort mapId, (int X, int Y) origin, IReadOnlyList<SlidePuzzleBlock> blocks, IReadOnlyList<int> grid, DateTimeOffset now)
    {
        Puzzle = puzzle ?? throw new ArgumentNullException(nameof(puzzle));
        ArgumentNullException.ThrowIfNull(blocks);
        ArgumentNullException.ThrowIfNull(grid);
        MapId = mapId;
        Origin = origin;
        _solved = blocks.Select(block => block.Id).ToArray();
        _grid = grid.ToArray();
        if (_grid.Length != _solved.Length) throw new ArgumentException("the grid and the block table differ in size", nameof(grid));
        var emptyId = blocks.Single(block => block.IsEmpty).Id;
        HoleIndex = Array.IndexOf(_grid, emptyId);
        StartedAt = now;
        Deadline = now.AddSeconds(puzzle.TimeLimitSeconds);
    }

    public SlidePuzzleEntry Puzzle { get; }
    public ushort MapId { get; }
    public (int X, int Y) Origin { get; }
    public int HoleIndex { get; private set; }
    public DateTimeOffset StartedAt { get; }
    public DateTimeOffset Deadline { get; }
    public uint? Result { get; private set; }

    public bool IsSolved => _grid.SequenceEqual(_solved);

    public void Arrive(int x, int y) => _standingOn = TileUnder(x, y);

    public SlideMove? StepAt(int x, int y)
    {
        if (Result is not null) return null;
        var tile = TileUnder(x, y);
        if (tile == _standingOn) return null;
        _standingOn = tile;
        if (tile is not { } stepped || !SlidePuzzleBoard.Adjacent(stepped, HoleIndex, Puzzle.Columns)) return null;
        var move = new SlideMove(stepped, HoleIndex);
        (_grid[stepped], _grid[HoleIndex]) = (_grid[HoleIndex], _grid[stepped]);
        HoleIndex = stepped;
        if (IsSolved) Result = PuzzleRules.Solved;
        return move;
    }

    public bool TimedOut(DateTimeOffset now)
    {
        if (Result is not null || now < Deadline) return false;
        Result = PuzzleRules.Failed;
        return true;
    }

    public uint RecordSeconds(DateTimeOffset now) => Result == PuzzleRules.Solved ? (uint)Math.Max(0, (now - StartedAt).TotalSeconds) : 0;

    private int? TileUnder(int x, int y) => SlidePuzzleBoard.TileAt(Origin.X, Origin.Y, Puzzle.Columns, Puzzle.Rows, x, y);
}
