using tPoseidon.Core.Game.Puzzle;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Game.Tests.Puzzle;

public sealed class SlidePuzzleRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly SlidePuzzleEntry Puzzle = new(1, "SlidePuzzleBlock_001", 3, 3, 600, 658, []);

    private static SlidePuzzleBlock[] Blocks(int count) =>
        Enumerable.Range(0, count).Select(id => new SlidePuzzleBlock(id, id == count - 1 ? PuzzleTables.EmptyBlockBundle : id)).ToArray();

    [Theory]
    [InlineData(3, 400, 384)]
    [InlineData(4, 320, 344)]
    [InlineData(5, 240, 304)]
    public void TheBoardCentreGivesTheOriginOfEachSize(int size, int x, int y)
    {
        Assert.Equal((x, y), SlidePuzzleBoard.Origin(640, 504, size, size));
    }

    [Fact]
    public void EveryTileCentreMapsBackToItsIndex()
    {
        var (originX, originY) = SlidePuzzleBoard.Origin(640, 504, 4, 4);
        for (var index = 0; index < 16; index++)
        {
            var (row, column) = (index / 4, index % 4);
            var anchorX = originX + ((4 - row) + column) * 80 - 80;
            var anchorY = originY + (row + column) * 40;
            var centre = SlidePuzzleBoard.TileCentre(originX, originY, 4, 4, index);
            Assert.Equal((anchorX + 80, anchorY + 40), centre);
            Assert.Equal(index, SlidePuzzleBoard.TileAt(originX, originY, 4, 4, centre.X, centre.Y));
            Assert.Equal(index, SlidePuzzleBoard.TileAt(originX, originY, 4, 4, centre.X + 70, centre.Y));
        }
        Assert.Null(SlidePuzzleBoard.TileAt(originX, originY, 4, 4, originX, originY));
    }

    [Fact]
    public void TheShuffleIsAnUnsolvedPermutationOfTheBlocks()
    {
        var random = new Random(7);
        var blocks = Blocks(9);
        for (var round = 0; round < 20; round++)
        {
            var grid = SlidePuzzleBoard.Shuffle(blocks, 3, 3, random.Next);
            Assert.Equal(Enumerable.Range(0, 9), grid.OrderBy(id => id));
            Assert.NotEqual(Enumerable.Range(0, 9), grid);
        }
    }

    [Fact]
    public void SteppingOnTheBlockNextToTheHoleMovesItAndSolvesTheBoard()
    {
        var origin = SlidePuzzleBoard.Origin(640, 504, 3, 3);
        var run = new SlidePuzzleRun(Puzzle, 657, origin, Blocks(9), [0, 1, 2, 3, 4, 5, 6, 8, 7], Now);
        Assert.Equal(7, run.HoleIndex);
        var centre = SlidePuzzleBoard.TileCentre(origin.X, origin.Y, 3, 3, 4);
        run.Arrive(centre.X, centre.Y);
        Assert.Null(run.StepAt(centre.X, centre.Y));
        var far = SlidePuzzleBoard.TileCentre(origin.X, origin.Y, 3, 3, 0);
        Assert.Null(run.StepAt(far.X, far.Y));
        var last = SlidePuzzleBoard.TileCentre(origin.X, origin.Y, 3, 3, 8);
        Assert.Equal(new SlideMove(8, 7), run.StepAt(last.X, last.Y));
        Assert.Equal(8, run.HoleIndex);
        Assert.Equal(PuzzleRules.Solved, run.Result);
        Assert.Equal(66U, run.RecordSeconds(Now.AddSeconds(66)));
    }

    [Fact]
    public void ArrivingNextToTheHoleDoesNotMoveABlock()
    {
        var origin = SlidePuzzleBoard.Origin(640, 504, 3, 3);
        var run = new SlidePuzzleRun(Puzzle, 657, origin, Blocks(9), [0, 1, 2, 3, 4, 5, 6, 8, 7], Now);
        var nextToHole = SlidePuzzleBoard.TileCentre(origin.X, origin.Y, 3, 3, 4);
        run.Arrive(nextToHole.X, nextToHole.Y);
        Assert.Null(run.StepAt(nextToHole.X, nextToHole.Y + 10));
        Assert.Null(run.Result);
    }

    [Fact]
    public void TheTimeLimitFailsTheRunWithNoRecord()
    {
        var run = new SlidePuzzleRun(Puzzle, 657, SlidePuzzleBoard.Origin(640, 504, 3, 3), Blocks(9), [0, 1, 2, 3, 4, 5, 6, 8, 7], Now);
        Assert.False(run.TimedOut(Now.AddSeconds(599)));
        Assert.True(run.TimedOut(Now.AddSeconds(600)));
        Assert.Equal(PuzzleRules.Failed, run.Result);
        Assert.Equal(0U, run.RecordSeconds(Now.AddSeconds(600)));
    }
}
