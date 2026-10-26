using System.Net;

namespace tPoseidon.Hosting.Tests;

public sealed class PlayAccountTests
{
    private sealed class FakeDirectory : IAccountDirectory
    {
        private readonly string[] _names;

        public FakeDirectory(params string[] names)
        {
            _names = names;
        }

        public string? Find(string username) => _names.FirstOrDefault(name => string.Equals(name, username, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<string> Names() => _names;
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public FakeHandler(HttpStatusCode status)
        {
            _status = status;
        }

        public string? Body { get; private set; }

        public Uri? Target { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Target = request.RequestUri;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_status);
        }
    }

    [Fact]
    public void ChoosingAnExistingAccountSavesItWithItsStoredSpelling()
    {
        using var temp = new TempFolder();
        var paths = new InstallPaths(temp.Path);
        var settings = new HostSettings();

        var chosen = PlayAccount.Choose(settings, new FakeDirectory("Alice", "bob"), "  alice ");
        settings.Save(paths);

        Assert.Equal("Alice", chosen);
        Assert.Equal("Alice", HostSettings.Load(paths)!.Launch.Account);
    }

    [Fact]
    public void AnUnknownAccountIsRefusedAndNothingChanges()
    {
        var settings = new HostSettings { Launch = { Account = "bob" } };

        var error = Assert.Throws<HostingException>(() => PlayAccount.Choose(settings, new FakeDirectory("bob"), "ghost"));

        Assert.Contains("There is no account named 'ghost'", error.Message, StringComparison.Ordinal);
        Assert.Equal("bob", settings.Launch.Account);
    }

    [Fact]
    public void AnEmptyAccountNameIsRefused()
    {
        var error = Assert.Throws<HostingException>(() => PlayAccount.Choose(new HostSettings(), new FakeDirectory("bob"), "  "));

        Assert.Contains("Say which account", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayWithoutAnAccountExplainsWhatToDo()
    {
        var error = Assert.Throws<HostingException>(() => GameClientLauncher.LaunchWithTicket(new HostSettings()));

        Assert.Contains("No game account is set for Play", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLaunchArgumentCarriesThePlayAccount()
    {
        var settings = new HostSettings { Launch = { Account = "Alice", ServiceName = ClientFolder.EnglishService }, Network = { PublicAddress = "192.0.2.10" } };

        Assert.StartsWith("gsp,Alice,desc,Trickster,Trickster,1,192.0.2.10|world|group|", GameClientLauncher.LaunchArgument(settings), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTicketRequestPostsTheLoginToTheServersWebPort()
    {
        var handler = new FakeHandler(HttpStatusCode.OK);
        using var http = new HttpClient(handler);
        var endpoint = PlayTicketClient.Endpoint("play.example.org", 9990, null);

        await PlayTicketClient.RequestAsync(http, endpoint, "Alice", "p&ss", CancellationToken.None);

        Assert.Equal("http://play.example.org:9990/play-ticket", handler.Target!.ToString());
        Assert.Equal("username=Alice&password=p%26ss", handler.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "wrong")]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many failed logins")]
    [InlineData(HttpStatusCode.BadGateway, "refused the login (502)")]
    public async Task ARefusedTicketRequestSaysWhy(HttpStatusCode status, string expected)
    {
        using var http = new HttpClient(new FakeHandler(status));

        var error = await Assert.ThrowsAsync<HostingException>(() =>
            PlayTicketClient.RequestAsync(http, PlayTicketClient.Endpoint("host", 9990, null), "Alice", "x", CancellationToken.None));

        Assert.Contains(expected, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AWebAddressOverridesTheDefaultAndMustBeValid()
    {
        Assert.Equal("https://play.example.org/play-ticket", PlayTicketClient.Endpoint("ignored", 9990, "https://play.example.org").ToString());
        Assert.Throws<HostingException>(() => PlayTicketClient.Endpoint("host", 9990, "not a url"));
    }
}
