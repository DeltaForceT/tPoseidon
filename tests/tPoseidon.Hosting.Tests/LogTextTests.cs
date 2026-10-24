using tPoseidon.Hosting.Logs;

namespace tPoseidon.Hosting.Tests;

public sealed class LogTextTests
{
    [Theory]
    [InlineData("[game:22006] zone 101 loaded", LogSeverity.Info)]
    [InlineData("[lobby] Unhandled exception in session", LogSeverity.Error)]
    [InlineData("[channel] failed to bind", LogSeverity.Error)]
    [InlineData("[master] warning: heartbeat late", LogSeverity.Warning)]
    [InlineData("[game] loaded 12 maps, 0 errors", LogSeverity.Info)]
    [InlineData("[game] no warnings", LogSeverity.Info)]
    public void ClassifiesBySeverityWords(string line, LogSeverity expected)
    {
        Assert.Equal(expected, LogClassifier.Classify(line));
    }

    [Fact]
    public void HidesTheGivenProfileInBothSlashForms()
    {
        var paths = new PersonalPaths(@"C:\Users\Sample");
        Assert.Equal(@"Loaded ~\data\x.bin and ~/y", paths.Hide(@"Loaded C:\Users\Sample\data\x.bin and C:/Users/Sample/y"));
    }

    [Fact]
    public void HidesOtherHomeFolders()
    {
        var paths = new PersonalPaths("/nonexistent-profile");
        Assert.Equal(@"~/x and ~/y and ~\z", paths.Hide(@"/home/someone/x and /Users/other/y and D:\Users\third\z"));
    }
}
