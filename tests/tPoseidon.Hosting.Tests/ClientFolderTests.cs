namespace tPoseidon.Hosting.Tests;

public sealed class ClientFolderTests
{
    [Fact]
    public void CompleteFolderPasses()
    {
        using var temp = new TempFolder();
        temp.WriteFile("Trickster.bin");
        temp.WriteFile(Path.Combine("data", "libconfig.dat"));
        temp.WriteFile(Path.Combine("data", "libconfig.idx"));
        temp.WriteFile(Path.Combine("data", "gulim12.fnt"));
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
        Assert.Equal(4, check.Problems.Count);
        Assert.Contains("no data/tahoma12.fnt or data/gulim12.fnt", check.Problems);
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
    public void AKoreanOnlyClientGetsCopiesOfItsOwnFilesForEnglishMode()
    {
        using var temp = new TempFolder();
        temp.WriteFile(Path.Combine("data", "libconfig.dat"), "config");
        temp.WriteFile(Path.Combine("data", "libconfig.idx"), "index");
        temp.WriteFile(Path.Combine("data", "gulim12.fnt"), "font");

        var copies = ClientFolder.PrepareEnglishMode(temp.Path);

        Assert.Equal(new[] { "libconfig_eng.dat", "libconfig_eng.idx", "tahoma12.fnt" }, copies.Select(copy => Path.GetFileName(copy.Target)));
        Assert.Equal("config", File.ReadAllText(temp.Combine("data", "libconfig_eng.dat")));
        Assert.Equal("index", File.ReadAllText(temp.Combine("data", "libconfig_eng.idx")));
        Assert.Equal("font", File.ReadAllText(temp.Combine("data", "tahoma12.fnt")));
        Assert.Empty(ClientFolder.MissingEnglishModeFiles(temp.Path));
    }

    [Fact]
    public void FilesTheClientAlreadyHasAreNeverReplaced()
    {
        using var temp = new TempFolder();
        temp.WriteFile(Path.Combine("data", "libconfig.dat"), "korean");
        temp.WriteFile(Path.Combine("data", "libconfig.idx"), "index");
        temp.WriteFile(Path.Combine("data", "libconfig_eng.dat"), "english");
        temp.WriteFile(Path.Combine("data", "libconfig_eng.idx"), "english index");
        temp.WriteFile(Path.Combine("data", "tahoma12.fnt"), "tahoma");
        temp.WriteFile(Path.Combine("data", "gulim12.fnt"), "gulim");

        Assert.Empty(ClientFolder.PrepareEnglishMode(temp.Path));
        Assert.Equal("english", File.ReadAllText(temp.Combine("data", "libconfig_eng.dat")));
        Assert.Equal("tahoma", File.ReadAllText(temp.Combine("data", "tahoma12.fnt")));
    }

    [Fact]
    public void KoreanTextIsRecognisedFromItsCharacterPairs()
    {
        var hangul = new byte[] { 0xC7, 0xD1, 0xB1, 0xDB };
        var korean = Enumerable.Repeat(hangul, ClientFolder.KoreanTextCharacters / 2).SelectMany(pair => pair).ToArray();
        var fewer = korean.AsSpan(0, korean.Length - 2).ToArray();
        var english = System.Text.Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("<CommonName>Guide</CommonName>", 500)));

        Assert.True(ClientFolder.IsKoreanText(korean));
        Assert.False(ClientFolder.IsKoreanText(fewer));
        Assert.False(ClientFolder.IsKoreanText(english));
    }

    [Fact]
    public void AnExplicitServiceIsKeptAndAClientWithoutDataStartsKorean()
    {
        using var temp = new TempFolder();
        Assert.Equal(ClientFolder.EnglishService, ClientFolder.ServiceFor(temp.Path, ClientFolder.EnglishService));
        Assert.Equal(ClientFolder.KoreanService, ClientFolder.ServiceFor(temp.Path, ClientFolder.AutomaticService));
    }

    [Fact]
    public void EnsureUsableThrowsForBrokenFolder()
    {
        using var temp = new TempFolder();
        Assert.Throws<HostingException>(() => ClientFolder.EnsureUsable(new HostSettings { ClientFolder = temp.Path }));
    }
}
