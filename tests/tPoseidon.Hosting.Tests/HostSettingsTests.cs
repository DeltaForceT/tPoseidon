namespace tPoseidon.Hosting.Tests;

public sealed class HostSettingsTests
{
    [Fact]
    public void MissingFileLoadsAsNull()
    {
        using var temp = new TempFolder();
        Assert.Null(HostSettings.Load(new InstallPaths(temp.Path)));
    }

    [Fact]
    public void LoadRequiredExplainsMissingSetup()
    {
        using var temp = new TempFolder();
        var error = Assert.Throws<HostingException>(() => HostSettings.LoadRequired(new InstallPaths(temp.Path)));
        Assert.Contains("not set up yet", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SaveThenLoadKeepsEveryValue()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var settings = new HostSettings
        {
            ClientFolder = "client",
            ClientExecutable = "Trickster.bin",
            ShopCash = "balance",
            ShopStartingCash = 500,
            Network = { Audience = PlayerAudience.Lan, PublicAddress = "192.0.2.10" },
            Ports = { Lobby = 19980, Game2 = 23007 },
            Database = { Connection = "Host=localhost;Port=5432;Database=tposeidon;Username=tposeidon" },
        };
        settings.Save(paths);

        var loaded = HostSettings.Load(paths)!;
        Assert.Equal("client", loaded.ClientFolder);
        Assert.Equal("Trickster.bin", loaded.ClientExecutable);
        Assert.Equal("balance", loaded.ShopCash);
        Assert.Equal(500, loaded.ShopStartingCash);
        Assert.Equal(PlayerAudience.Lan, loaded.Network.Audience);
        Assert.Equal("0.0.0.0", loaded.Network.BindAddress);
        Assert.Equal(19980, loaded.Ports.Lobby);
        Assert.Equal(23007, loaded.Ports.Game2);
        Assert.Equal("tposeidon@localhost:5432/tposeidon", loaded.Database.Describe());
        Assert.False(File.Exists(paths.SettingsFile + ".tmp"));
    }

    [Fact]
    public void BrokenFileIsExplained()
    {
        using var temp = new TempFolder();
        temp.WriteFile("tposeidon.json", "{ \"clientFolder\": \"C:\\x\" }");
        var error = Assert.Throws<HostingException>(() => HostSettings.Load(new InstallPaths(temp.Path)));
        Assert.Contains("is not valid settings", error.Summary, StringComparison.Ordinal);
        Assert.NotNull(error.Hint);
    }

    [Fact]
    public void LocalAudienceBindsToLoopback()
    {
        Assert.Equal("127.0.0.1", new NetworkSettings { Audience = PlayerAudience.Local }.BindAddress);
    }
}
