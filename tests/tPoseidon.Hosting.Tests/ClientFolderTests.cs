namespace tPoseidon.Hosting.Tests;

public sealed class ClientFolderTests
{
    [Fact]
    public void CompleteFolderPasses()
    {
        using var temp = new TempFolder();
        temp.WriteFile("Trickster.bin");
        temp.WriteFile(Path.Combine("data", "libconfig.dat"));
        Directory.CreateDirectory(temp.Combine("data", "map"));

        var check = ClientFolder.Check(temp.Path);

        Assert.True(check.Ok);
        Assert.Equal("Trickster.bin", check.Executable);
        Assert.Empty(check.Problems);
    }

    [Fact]
    public void EmptyFolderListsEveryProblem()
    {
        using var temp = new TempFolder();
        var check = ClientFolder.Check(temp.Path);
        Assert.False(check.Ok);
        Assert.Contains(check.Problems, problem => problem.Contains("Trickster.bin", StringComparison.Ordinal));
        Assert.Contains("no data folder", check.Problems);
    }

    [Fact]
    public void MissingMapsAndLibConfigAreReported()
    {
        using var temp = new TempFolder();
        temp.WriteFile("Trickster.exe");
        Directory.CreateDirectory(temp.Combine("data"));
        var check = ClientFolder.Check(temp.Path);
        Assert.False(check.Ok);
        Assert.Equal("Trickster.exe", check.Executable);
        Assert.Equal(2, check.Problems.Count);
    }

    [Fact]
    public void MissingFolderIsOneProblem()
    {
        using var temp = new TempFolder();
        var check = ClientFolder.Check(temp.Combine("nowhere"));
        Assert.False(check.Ok);
        Assert.Single(check.Problems);
    }

    [Fact]
    public void EnglishServicePrefersEnglishLibConfig()
    {
        using var temp = new TempFolder();
        temp.WriteFile(Path.Combine("data", "libconfig.dat"));
        var english = temp.WriteFile(Path.Combine("data", "libconfig_eng.dat"));
        Assert.Equal(english, ClientFolder.LibConfigFor(temp.Path, "gsp"));
        Assert.EndsWith("libconfig.dat", ClientFolder.LibConfigFor(temp.Path, "kr"), StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureUsableThrowsForBrokenFolder()
    {
        using var temp = new TempFolder();
        Assert.Throws<HostingException>(() => ClientFolder.EnsureUsable(new HostSettings { ClientFolder = temp.Path }));
    }
}
