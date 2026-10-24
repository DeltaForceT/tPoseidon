using System.Net;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using tPoseidon.Hosting;

[assembly: AvaloniaTestApplication(typeof(tPoseidon.Panel.Tests.TestApp))]

namespace tPoseidon.Panel.Tests;

public static class TestApp
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class PanelTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "tposeidon-panel-" + Guid.NewGuid().ToString("N"));

    public PanelTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void LaunchOptionsAreParsed()
    {
        var options = LaunchOptions.Parse(["--home", "x", "--setup", "--no-splash", "--reduce-animations"]);
        Assert.Equal("x", options.Home);
        Assert.True(options.Setup && options.NoSplash && options.Reduce);
        Assert.Null(options.Problem);
        Assert.NotNull(LaunchOptions.Parse(["--bogus"]).Problem);
    }

    [AvaloniaFact]
    public void EveryIconHasADrawnShape()
    {
        foreach (var glyph in new[] { Glyph.Play, Glyph.Stop, Glyph.Restart, Glyph.Log, Glyph.More, Glyph.Client, Glyph.Copy, Glyph.Clear, Glyph.Check, Glyph.Warn, Glyph.Error, Glyph.Info, Glyph.Shield, Glyph.Admin })
        {
            var view = Glyph.Drawn(glyph, 12, Tokens.TextPrimary, out var path);
            Assert.NotNull(view);
            Assert.NotNull(path.Data);
        }
    }

    [AvaloniaFact]
    public void ClientPageRejectsAFolderWithoutTheClient()
    {
        var state = new SetupState(new InstallPaths(_root), new HostSettings());
        var page = new ClientPage(state);
        Box(page, 0).Text = _root;
        Assert.False(page.Validate());
        Assert.Contains("bad", Box(page, 0).Classes);
    }

    [AvaloniaFact]
    public void ClientPageAcceptsTheClientAndRemembersIt()
    {
        var client = Directory.CreateDirectory(Path.Combine(_root, "client")).FullName;
        File.WriteAllText(Path.Combine(client, "Trickster.bin"), "");
        Directory.CreateDirectory(Path.Combine(client, "data", "map"));
        File.WriteAllText(Path.Combine(client, "data", "libconfig.dat"), "");
        var state = new SetupState(new InstallPaths(_root), new HostSettings());
        var page = new ClientPage(state);
        Box(page, 0).Text = client;
        Assert.True(page.Validate());
        Assert.Equal(client, state.Settings.ClientFolder);
        Assert.Equal("Trickster.bin", state.Settings.ClientExecutable);
    }

    [AvaloniaFact]
    public void AccountPagePlaysWithTheCreatedAccountUnlessAnotherIsChosen()
    {
        var state = new SetupState(new InstallPaths(_root), new HostSettings());
        var page = new AccountPage(state);
        Box(page, 0).Text = "Alice";
        Box(page, 1).Text = "secret";
        Box(page, 2).Text = "secret";
        Assert.True(page.Validate());
        Assert.Equal("", state.PlayWith);

        page.SelectPlay(true);
        Assert.False(page.Validate());
        Box(page, 3).Text = "Bob";
        Assert.True(page.Validate());
        Assert.Equal("Bob", state.PlayWith);
    }

    [AvaloniaFact]
    public void PortsPageRejectsDuplicates()
    {
        var state = new SetupState(new InstallPaths(_root), new HostSettings());
        var page = new PortsPage(state);
        Box(page, 0).Text = "40001";
        Box(page, 1).Text = "40001";
        Assert.False(page.Validate());
        Assert.Contains("bad", Box(page, 0).Classes);
        Assert.Contains("bad", Box(page, 1).Classes);
    }

    [AvaloniaFact]
    public void PortsPageWritesFreePortsToTheSettings()
    {
        var state = new SetupState(new InstallPaths(_root), new HostSettings());
        var page = new PortsPage(state);
        var boxes = page.GetLogicalDescendants().OfType<TextBox>().ToList();
        var used = new HashSet<int>();
        foreach (var box in boxes)
        {
            int port;
            do
            {
                port = FreePort();
            }
            while (!used.Add(port));
            box.Text = port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        Assert.True(page.Validate());
        Assert.Equal(int.Parse(boxes[0].Text!, System.Globalization.CultureInfo.InvariantCulture), state.Settings.Ports.Lobby);
    }

    [AvaloniaFact]
    public async Task PlayersPageNeedsAValidAddress()
    {
        var state = new SetupState(new InstallPaths(_root), new HostSettings());
        var page = new PlayersPage(state);
        page.Select(PlayerAudience.Internet);
        Box(page, 0).Text = "http://bad host:1";
        Assert.False(await page.LeaveAsync());
        Box(page, 0).Text = "203.0.113.7";
        Assert.True(await page.LeaveAsync());
        Assert.Equal(PlayerAudience.Internet, state.Settings.Network.Audience);
        Assert.Equal("203.0.113.7", state.Settings.Network.PublicAddress);
    }

    [AvaloniaFact]
    public async Task DatabasePageExplainsAClosedPort()
    {
        var state = new SetupState(new InstallPaths(_root), new HostSettings());
        var page = new DatabasePage(state);
        Box(page, 0).Text = "127.0.0.1";
        Box(page, 1).Text = FreePort().ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(await page.RunTestAsync());
        Assert.Contains("bad", Box(page, 0).Classes);
        Assert.Equal("", state.AdminConnection);
    }

    [AvaloniaFact]
    public void MainWindowShowsTheDatabaseAndEveryServer()
    {
        var paths = new InstallPaths(_root);
        new HostSettings { Database = { Connection = "Host=127.0.0.1;Port=1;Database=tposeidon;Username=tposeidon" } }.Save(paths);
        var host = new PanelHost(paths);
        var window = new MainWindow(host, new PanelPreferences());
        host.Reload();
        Assert.True(host.IsSetUp);
        Assert.Equal(8, window.GetLogicalDescendants().OfType<ServiceRow>().Count());
        Assert.Equal(ServiceModel.DatabaseName, host.Services[0].Name);
        Assert.Equal(1, host.Services[0].Port);
        host.End();
    }

    private static TextBox Box(Control page, int index) => page.GetLogicalDescendants().OfType<TextBox>().ElementAt(index);

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
