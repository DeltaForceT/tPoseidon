using tPoseidon.Hosting.Servers;

namespace tPoseidon.Hosting.Tests;

public sealed class ServerCatalogTests
{
    [Fact]
    public void EveryServerReadsAndWritesItsOwnPort()
    {
        var ports = new PortSettings();
        var value = 30000;
        foreach (var server in ServerCatalog.All)
        {
            server.SetPort(ports, value);
            Assert.Equal(value, server.Port(ports));
            value++;
        }

        Assert.Equal(ServerCatalog.All.Count, ServerCatalog.All.Select(server => server.Port(ports)).Distinct().Count());
    }

    [Fact]
    public void ShopIsLeftOutWhenDisabled()
    {
        var settings = new HostSettings { ShopEnabled = false };
        Assert.DoesNotContain(ServerCatalog.Enabled(settings), server => server.IsShop);
        Assert.DoesNotContain(settings.Ports.Shop, ServerCatalog.PlayerFacingPorts(settings));
    }

    [Fact]
    public void PlayerFacingPortsSkipTheMaster()
    {
        var settings = new HostSettings();
        var ports = ServerCatalog.PlayerFacingPorts(settings);
        Assert.DoesNotContain(settings.Ports.Master, ports);
        Assert.Contains(settings.Ports.Lobby, ports);
        Assert.Contains(settings.Ports.Shop, ports);
    }

    [Fact]
    public void NamesAreUnique()
    {
        Assert.Equal(ServerCatalog.All.Count, ServerCatalog.All.Select(server => server.Name).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ServerCatalog.All.Count, ServerCatalog.All.Select(server => server.ShortName).Distinct(StringComparer.Ordinal).Count());
    }
}
