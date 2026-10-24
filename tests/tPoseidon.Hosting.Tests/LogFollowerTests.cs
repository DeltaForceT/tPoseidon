using System.Text;
using tPoseidon.Hosting.Logs;

namespace tPoseidon.Hosting.Tests;

public sealed class LogFollowerTests
{
    [Fact]
    public void MissingFileGivesNothing()
    {
        using var temp = new TempFolder();
        Assert.Empty(new LogFollower(temp.Combine("none.log")).ReadNew());
    }

    [Fact]
    public void ReadsHistoryThenOnlyNewLines()
    {
        using var temp = new TempFolder();
        var path = temp.WriteFile("a.log", "one\ntwo\n");
        var follower = new LogFollower(path);

        Assert.Equal(new[] { "one", "two" }, follower.ReadNew());
        Assert.Empty(follower.ReadNew());

        File.AppendAllText(path, "three\r\nfour\n");
        Assert.Equal(new[] { "three", "four" }, follower.ReadNew());
    }

    [Fact]
    public void HoldsAnUnfinishedLine()
    {
        using var temp = new TempFolder();
        var path = temp.WriteFile("a.log", "");
        var follower = new LogFollower(path);
        Assert.Empty(follower.ReadNew());

        File.AppendAllText(path, "par");
        Assert.Empty(follower.ReadNew());
        File.AppendAllText(path, "tial\n");
        Assert.Equal(new[] { "partial" }, follower.ReadNew());
    }

    [Fact]
    public void HistoryStartsAtALineBoundary()
    {
        using var temp = new TempFolder();
        var path = temp.WriteFile("a.log", "aaaaaaaaaa\nbbb\nccc\n");
        var follower = new LogFollower(path, historyBytes: 10);
        Assert.Equal(new[] { "bbb", "ccc" }, follower.ReadNew());
    }

    [Fact]
    public void HistoryThatStartsOnALineKeepsIt()
    {
        using var temp = new TempFolder();
        var path = temp.WriteFile("a.log", "aaaa\nbbb\n");
        var follower = new LogFollower(path, historyBytes: 4);
        Assert.Equal(new[] { "bbb" }, follower.ReadNew());
    }

    [Fact]
    public void TruncatedFileIsReadFromTheStart()
    {
        using var temp = new TempFolder();
        var path = temp.WriteFile("a.log", "first run line\n");
        var follower = new LogFollower(path);
        follower.ReadNew();

        File.WriteAllText(path, "new\n");
        Assert.Equal(new[] { "new" }, follower.ReadNew());
    }

    [Fact]
    public void KeepsMultiByteTextSplitAcrossWrites()
    {
        using var temp = new TempFolder();
        var path = temp.WriteFile("a.log", "");
        var follower = new LogFollower(path);
        follower.ReadNew();
        var bytes = Encoding.UTF8.GetBytes("café 맵\n");
        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            stream.Write(bytes, 0, 4);
        }

        Assert.Empty(follower.ReadNew());
        using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
        {
            stream.Write(bytes, 4, bytes.Length - 4);
        }

        Assert.Equal(new[] { "café 맵" }, follower.ReadNew());
    }
}
