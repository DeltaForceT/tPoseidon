using tPoseidon.Core.Game.Puzzle;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PuzzleFramesTests
{
    [Fact]
    public void SlideStartWritesTheIdOriginCountAndBlocks()
    {
        var bytes = PuzzleFrames.SlideStart(1, (400, 384), [3, 0, 8]);
        Assert.Equal(new byte[] { 1, 0, 0x90, 0x01, 0x80, 0x01, 3, 0, 3, 0, 0, 0, 8, 0 }, bytes);
    }

    [Fact]
    public void SlideMoveWritesFromThenTo()
    {
        Assert.Equal(new byte[] { 8, 0, 7, 0 }, PuzzleFrames.SlideMove(new SlideMove(8, 7)));
    }

    [Fact]
    public void ResultWritesTheResultThenTheRecord()
    {
        Assert.Equal(new byte[] { 0, 0, 0, 0, 66, 0, 0, 0 }, PuzzleFrames.Result(PuzzleRules.Solved, 66));
    }
}
