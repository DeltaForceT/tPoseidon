namespace tPoseidon.Hosting.Tests;

public sealed class InstallPathsTests
{
    [Fact]
    public void AppFolderResolvesToItsParent()
    {
        using var temp = new TempFolder();
        var app = Directory.CreateDirectory(temp.Combine("app")).FullName;
        var paths = InstallPaths.FromBaseDirectory(app + Path.DirectorySeparatorChar);
        Assert.Equal(Path.GetFullPath(temp.Path), paths.Root);
        Assert.Equal(Path.Combine(paths.Root, "tposeidon.json"), paths.SettingsFile);
    }

    [Fact]
    public void OtherFolderIsItsOwnRoot()
    {
        using var temp = new TempFolder();
        var paths = InstallPaths.FromBaseDirectory(temp.Path);
        Assert.Equal(Path.GetFullPath(temp.Path), paths.Root);
    }

    [Fact]
    public void ExplicitRootWins()
    {
        using var temp = new TempFolder();
        var paths = InstallPaths.Resolve(" " + temp.Path + " ");
        Assert.Equal(Path.GetFullPath(temp.Path), paths.Root);
        Assert.Equal(Path.Combine(paths.Root, "log", "Kr-Lobby.log"), paths.LogFile("Kr-Lobby"));
        Assert.Equal(Path.Combine(paths.Root, "run", "Kr-Lobby.pid"), paths.PidFile("Kr-Lobby"));
    }
}
