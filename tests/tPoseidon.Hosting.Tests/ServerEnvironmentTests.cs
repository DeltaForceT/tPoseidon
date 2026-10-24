using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Tests;

public sealed class ServerEnvironmentTests
{
    [Fact]
    public void GameServerGetsItsPortAndLogFile()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var settings = new HostSettings { Ports = { Game1 = 22100 } };
        var game = ServerCatalog.All.Single(server => server.Name == "Kr-Game1");

        var environment = ServerEnvironment.For(game, settings, paths);

        Assert.Equal(paths.LogFile("Kr-Game1"), environment["MT_LOG_FILE"]);
        Assert.Equal("22100", environment["MT_GAME_PORT"]);
        Assert.Equal("127.0.0.1", environment["MT_BIND_ADDRESS"]);
        Assert.False(environment.ContainsKey("MT_CASH_MODE"));
        Assert.Equal(new[] { "22100" }, ServerEnvironment.Arguments(game, settings));
    }

    [Fact]
    public void ShopGetsCashSettingsAndNoArguments()
    {
        using var temp = new TempFolder();
        var settings = new HostSettings { ShopCash = "balance", ShopStartingCash = 42 };
        var shop = ServerCatalog.All.Single(server => server.IsShop);

        var environment = ServerEnvironment.For(shop, settings, new InstallPaths(temp.Path));

        Assert.Equal("balance", environment["MT_CASH_MODE"]);
        Assert.Equal("42", environment["MT_CASH_START"]);
        Assert.Equal($"http://127.0.0.1:{settings.Ports.Shop}", environment["MT_WEBSHOP_URLS"]);
        Assert.Empty(ServerEnvironment.Arguments(shop, settings));
    }
}
