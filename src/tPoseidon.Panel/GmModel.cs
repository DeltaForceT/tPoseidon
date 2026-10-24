using tPoseidon.Core.Data;
using tPoseidon.Core.Data.Postgres;
using tPoseidon.Core.Net.Social;
using tPoseidon.Hosting;
using tPoseidon.Hosting.GameMaster;

namespace tPoseidon.Panel;

public enum GmEntryKind
{
    Done,
    Refused,
    Failed,
}

public sealed record GmEntry(DateTimeOffset At, string Title, GmEntryKind Kind, IReadOnlyList<string> Lines);

public sealed class GmServices
{
    public required IGmToolTransport Transport { get; init; }

    public GmToolEndpoint? Endpoint { get; init; }

    public IGmAccountBook? Accounts { get; init; }

    public IGmPenaltyBook? Penalties { get; init; }

    public required Func<GameCatalog> Catalog { get; init; }

    public string? SetupProblem { get; init; }

    public static GmServices For(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        HostSettings? settings;
        try
        {
            settings = HostSettings.Load(paths);
        }
        catch (HostingException ex)
        {
            return Unconfigured(ex.Summary);
        }

        if (settings is null)
        {
            return Unconfigured($"tPoseidon is not set up yet ({paths.SettingsFile} is missing). Run the first-time setup from the main window.");
        }

        return new GmServices
        {
            Transport = GmToolTransport.Shared,
            Endpoint = GmToolEndpoint.For(settings),
            Accounts = new DatabaseGmAccountBook(settings.Database),
            Penalties = new DatabasePenaltyBook(settings.Database),
            Catalog = () => GameCatalog.For(settings),
        };
    }

    private static GmServices Unconfigured(string problem) => new()
    {
        Transport = GmToolTransport.Shared,
        Catalog = () => GameCatalog.Empty(problem),
        SetupProblem = problem,
    };
}

public sealed class GmModel : IDisposable
{
    public const int HistoryLimit = 100;
    public const int CharacterSearchLimit = 40;

    private readonly GmServices _services;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<GmEntry> _history = [];
    private readonly HashSet<string> _wired = new(StringComparer.Ordinal);
    private Task<GameCatalog>? _catalog;
    private GmLogin? _login;
    private int _running;

    public GmModel(GmServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        Link = services.SetupProblem is { } problem ? new GmLinkState(GmLinkStatus.NotSetUp, problem) : GmLink.NotConnected;
    }

    public event Action? Changed;

    public event Action<string>? CharacterChosen;

    public GmLinkState Link { get; private set; }

    public bool Busy => _running > 0;

    public string? Running { get; private set; }

    public GmToolEndpoint? Endpoint => _services.Endpoint;

    public string? GameMaster => _login?.Character;

    public IReadOnlyList<GmEntry> History => _history;

    public IReadOnlyCollection<string> Wired => _wired;

    public void Wire(string name) => _wired.Add(name);

    public void Choose(string character) => CharacterChosen?.Invoke(character);

    public Task<GameCatalog> CatalogAsync() => _catalog ??= Task.Run(_services.Catalog);

    public async Task ConnectAsync(string character, string password)
    {
        ArgumentNullException.ThrowIfNull(character);
        ArgumentNullException.ThrowIfNull(password);
        if (_services.Endpoint is not { } endpoint)
        {
            return;
        }

        var login = new GmLogin(character.Trim(), password);
        await RunAsync("Connecting", async () =>
        {
            Link = await Task.Run(() => GmLink.CheckAsync(_services.Transport, endpoint, login, _services.Accounts, CancellationToken.None)).ConfigureAwait(true);
            _login = Link.Connected ? login : null;
            return null;
        }).ConfigureAwait(true);
    }

    public void Disconnect()
    {
        _login = null;
        Link = GmLink.NotConnected;
        Changed?.Invoke();
    }

    public Task<GmOutcome?> ToolAsync(string title, string command, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(arguments);
        var tool = GameMasterToolCommands.Find(command) ?? throw new ArgumentException($"'{command}' is not a GM tool command.", nameof(command));
        if (_login is not { } login || _services.Endpoint is not { } endpoint)
        {
            return Fail(title, Link.Connected ? Link.Message : "Connect first: " + Link.Message);
        }

        GmToolCall call;
        try
        {
            call = GmTool.Prepare(tool, arguments);
        }
        catch (HostingException ex)
        {
            return Fail(title, ex.Message);
        }

        return RunAsync(title, async () =>
        {
            try
            {
                return await Task.Run(() => GmTool.RunAsync(_services.Transport, endpoint, login, call, CancellationToken.None)).ConfigureAwait(true);
            }
            catch (HostingException)
            {
                Link = await Task.Run(() => GmLink.CheckAsync(_services.Transport, endpoint, login, _services.Accounts, CancellationToken.None)).ConfigureAwait(true);
                if (!Link.Connected)
                {
                    _login = null;
                }

                throw;
            }
        });
    }

    public Task<GmOutcome?> PenaltyAsync(string title, string subcommand, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!GmPenalties.Handles(subcommand))
        {
            throw new ArgumentException($"'{subcommand}' is not a penalty command.", nameof(subcommand));
        }

        return _services.Penalties is { } book
            ? RunAsync(title, () => Task.Run(() => (GmOutcome?)GmPenalties.Run(book, subcommand, arguments)))
            : Fail(title, Link.Message);
    }

    public Task<GmOutcome?> AccountsAsync(string title, Func<IGmAccountBook, GmOutcome> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return _services.Accounts is { } book
            ? RunAsync(title, () => Task.Run(() => (GmOutcome?)action(book)))
            : Fail(title, Link.Message);
    }

    public async Task<IReadOnlyList<CharacterSummary>> FindCharactersAsync(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_services.Accounts is not { } book)
        {
            await Fail("Find characters", Link.Message).ConfigureAwait(true);
            return [];
        }

        try
        {
            return await Task.Run(() => book.FindCharacters(text, CharacterSearchLimit)).ConfigureAwait(true);
        }
        catch (HostingException ex)
        {
            await Fail("Find characters", ex.Message).ConfigureAwait(true);
            return [];
        }
    }

    public async Task<IReadOnlyList<GmCharacterEntry>> GameMastersAsync()
    {
        if (_services.Accounts is not { } book)
        {
            return [];
        }

        try
        {
            return await Task.Run(book.List).ConfigureAwait(true);
        }
        catch (HostingException ex)
        {
            await Fail("Registered GM characters", ex.Message).ConfigureAwait(true);
            return [];
        }
    }

    public async Task<IReadOnlyList<PenaltyKind>> PenaltyKindsAsync()
    {
        if (_services.Penalties is not { } book)
        {
            return [];
        }

        try
        {
            return await Task.Run(book.Kinds).ConfigureAwait(true);
        }
        catch (HostingException ex)
        {
            await Fail("Penalty kinds", ex.Message).ConfigureAwait(true);
            return [];
        }
    }

    public void Dispose() => _gate.Dispose();

    public void ClearHistory()
    {
        _history.Clear();
        Changed?.Invoke();
    }

    public Task<GmOutcome?> Fail(string title, string message)
    {
        Record(new GmEntry(DateTimeOffset.Now, title, GmEntryKind.Failed, [message]));
        return Task.FromResult<GmOutcome?>(null);
    }

    private async Task<GmOutcome?> RunAsync(string title, Func<Task<GmOutcome?>> work)
    {
        Interlocked.Increment(ref _running);
        Running = title;
        Changed?.Invoke();
        await _gate.WaitAsync().ConfigureAwait(true);
        try
        {
            Running = title;
            Changed?.Invoke();
            var outcome = await work().ConfigureAwait(true);
            if (outcome is not null)
            {
                Record(Entry(title, outcome));
            }

            return outcome;
        }
        catch (HostingException ex)
        {
            Record(new GmEntry(DateTimeOffset.Now, title, GmEntryKind.Failed, [ex.Message]));
            return null;
        }
        finally
        {
            _gate.Release();
            Interlocked.Decrement(ref _running);
            Running = Busy ? Running : null;
            Changed?.Invoke();
        }
    }

    private static GmEntry Entry(string title, GmOutcome outcome)
    {
        var lines = outcome.Results.ToList();
        if (lines.Count == 0)
        {
            lines.Add(outcome.Ok ? "Sent. The server does not answer this command; check in game that it happened." : "The server did not accept the GM login.");
        }

        var kind = outcome.Refused || !outcome.Ok ? GmEntryKind.Refused : GmEntryKind.Done;
        return new GmEntry(DateTimeOffset.Now, title, kind, lines);
    }

    private void Record(GmEntry entry)
    {
        _history.Insert(0, entry);
        if (_history.Count > HistoryLimit)
        {
            _history.RemoveAt(_history.Count - 1);
        }

        Changed?.Invoke();
    }
}
