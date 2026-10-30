using tPoseidon.Hosting;
using tPoseidon.Hosting.ClientScan;
using tPoseidon.Ui;

namespace tPoseidon.Panel;

public sealed record ClientScanNotice(BannerKind Kind, string Title, string? Detail);

public sealed class ClientScanModel
{
    private readonly ClientScanner _scanner;
    private ClientScanContext? _context;

    public ClientScanModel(ClientScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(scanner);
        _scanner = scanner;
    }

    public event Action? Changed;

    public ScanReport? Report { get; private set; }

    public ClientScanNotice? Notice { get; private set; }

    public bool Busy { get; private set; }

    public bool CanRestore { get; private set; }

    public bool HasFixes => Report is { FixableIds.Count: > 0 };

    public string FixTitle(string fixId) => Report is { } report && report.FixTitles.TryGetValue(fixId, out var title) ? title : fixId;

    public async Task ScanAsync(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        await RunAsync(() => Task.Run(() => Refresh(context))).ConfigureAwait(true);
    }

    public Task FixAllAsync() => FixAsync(null);

    public async Task FixAsync(IReadOnlyCollection<string>? fixIds)
    {
        var context = _context ?? throw new InvalidOperationException("Scan the client folder first.");
        await RunAsync(() => Task.Run(() =>
        {
            var results = _scanner.Fix(context, fixIds);
            Refresh(context);
            Notice = Describe(results, "Nothing needed fixing.");
        })).ConfigureAwait(true);
    }

    public async Task RestoreAsync()
    {
        var context = _context ?? throw new InvalidOperationException("Scan the client folder first.");
        await RunAsync(() => Task.Run(() =>
        {
            var results = _scanner.Restore(context);
            Refresh(context);
            Notice = Describe(results, "There is nothing to restore.");
        })).ConfigureAwait(true);
    }

    private void Refresh(ClientScanContext context)
    {
        Report = _scanner.Scan(context);
        CanRestore = _scanner.CanRestore(context);
        Notice = null;
    }

    private async Task RunAsync(Func<Task> work)
    {
        Busy = true;
        Notice = null;
        Changed?.Invoke();
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (HostingException ex)
        {
            Notice = new ClientScanNotice(BannerKind.Error, ex.Summary, ex.Hint);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Notice = new ClientScanNotice(BannerKind.Error, "The client folder could not be read.", ex.Message);
        }
        finally
        {
            Busy = false;
            Changed?.Invoke();
        }
    }

    private static ClientScanNotice Describe(IReadOnlyList<FixResult> results, string nothing) =>
        results.Any(result => result.Outcome.Changed)
            ? new ClientScanNotice(BannerKind.Success, "Done", string.Join(Environment.NewLine, results.Where(result => result.Outcome.Changed).Select(result => result.Outcome.Message)))
            : new ClientScanNotice(BannerKind.Info, nothing, null);
}
