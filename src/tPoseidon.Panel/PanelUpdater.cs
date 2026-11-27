using Avalonia.Threading;
using tPoseidon.Hosting;
using tPoseidon.Hosting.Updates;

namespace tPoseidon.Panel;

public sealed class PanelUpdater : IDisposable
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);

    private readonly PanelHost _host;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(30) };

    private readonly DispatcherTimer _timer = new() { Interval = Tick };
    private UpdateService? _service;
    private HostSettings? _serviceSettings;
    private DateTime? _lastCheck;
    private DateTime? _lastAutomatic;
    private CancellationTokenSource? _cancel;
    private string? _dismissed;
    private bool _checking;

    public PanelUpdater(PanelHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _timer.Tick += (_, _) => OnTick();
    }

    public event Action? Changed;

    public event Action? ExitRequested;

    public UpdateOffer? Offer { get; private set; }

    public bool Running { get; private set; }

    public string? Status { get; private set; }

    public string? Problem { get; private set; }

    public bool Visible => Offer is not null && Offer.Version.ToString() != _dismissed;

    public UpdateMode Mode => _host.Settings?.Updates.Mode ?? UpdateMode.OneClick;

    public void Begin()
    {
        UpdateHelper.RemoveFinished();
        if (UpdateService.TakeUnseenResult(_host.Paths) is { } result)
        {
            var kind = result.Outcome switch
            {
                UpdateOutcome.Updated => BannerKind.Success,
                UpdateOutcome.NotStarted => BannerKind.Warning,
                _ => BannerKind.Error,
            };
            _host.Notify(kind, result.Message, $"Details: {new UpdateWork(_host.Paths).LogFile}");
            LogHub.Write(LogHub.PanelSource, kind == BannerKind.Error ? LogLevel.Error : LogLevel.Info, result.Message);
        }

        _timer.Start();
        _ = CheckAsync(manual: false);
    }

    public void End()
    {
        _timer.Stop();
        _cancel?.Cancel();
    }

    public void Dismiss()
    {
        _dismissed = Offer?.Version.ToString();
        Changed?.Invoke();
    }

    public void CancelRunning() => _cancel?.Cancel();

    public void Dispose()
    {
        _timer.Stop();
        _cancel?.Dispose();
    }

    public async Task CheckAsync(bool manual)
    {
        if (_checking || Running || _host.Settings is not { } settings)
        {
            return;
        }

        _checking = true;
        try
        {
            _lastCheck = DateTime.UtcNow;
            var check = await Service(settings).CheckAsync(CancellationToken.None).ConfigureAwait(true);
            Problem = check.Problem;
            Offer = check.Offer;
            if (check.Offer is not null && manual)
            {
                _dismissed = null;
            }

            if (manual)
            {
                _host.Notify(
                    check.Failed ? BannerKind.Warning : BannerKind.Info,
                    check.Failed ? "The check for updates failed." : check.Offer is null ? "You are up to date." : $"Version {check.Offer.Version} is available.",
                    check.Problem);
            }
            else if (check.Failed)
            {
                LogHub.Write(LogHub.PanelSource, LogLevel.Warn, "Update check: " + check.Problem);
            }
        }
        finally
        {
            _checking = false;
            Changed?.Invoke();
        }
    }

    public async Task InstallAsync()
    {
        if (Running || Offer is not { } offer || _host.Settings is not { } settings)
        {
            return;
        }

        Running = true;
        _cancel = new CancellationTokenSource();
        SetStatus($"Downloading tPoseidon {offer.Version}...");
        try
        {
            var service = Service(settings);
            var panel = _host.Paths.AppExecutable("tPoseidon.Panel");
            var progress = new Progress<double>(done => SetStatus($"Downloading tPoseidon {offer.Version}: {done:P0}"));
            await service.InstallAsync(
                offer,
                service.OptionsFor(true, panel),
                new SystemUpdateHost(_host.Paths, settings),
                Environment.ProcessId,
                progress,
                line => Dispatcher.UIThread.Post(() => SetStatus(line)),
                null,
                _cancel.Token).ConfigureAwait(true);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Installing tPoseidon {offer.Version}. The panel closes and opens again by itself.");
            ExitRequested?.Invoke();
        }
        catch (OperationCanceledException)
        {
            SetStatus(null);
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, "The update was cancelled.");
            _host.Notify(BannerKind.Info, "The update was cancelled.", "Nothing was changed.");
        }
        catch (HostingException ex)
        {
            SetStatus(null);
            LogHub.Write(LogHub.PanelSource, LogLevel.Error, ex.Summary);
            _host.Notify(BannerKind.Error, ex.Summary, ex.Hint?.Split('\n')[0].Trim());
        }
        finally
        {
            Running = false;
            _cancel?.Dispose();
            _cancel = null;
            SetStatus(null);
        }
    }

    private void SetStatus(string? text)
    {
        Status = text;
        Changed?.Invoke();
    }

    private UpdateService Service(HostSettings settings)
    {
        if (_service is null || !ReferenceEquals(_serviceSettings, settings))
        {
            _service = UpdateService.Create(_host.Paths, settings, Http);
            _serviceSettings = settings;
        }

        return _service;
    }

    private void OnTick()
    {
        if (_host.Settings is not { } settings || Running || _checking)
        {
            return;
        }

        if (_lastCheck is null || DateTime.UtcNow - _lastCheck >= UpdateService.CheckEvery)
        {
            _ = CheckAsync(manual: false);
            return;
        }

        if (Offer is { } offer
            && UpdateService.AutomaticDue(settings.Updates, DateTime.Now, _lastAutomatic)
            && !UpdateService.FailedBefore(_host.Paths, offer.Version.ToString()))
        {
            _lastAutomatic = DateTime.Now;
            LogHub.Write(LogHub.PanelSource, LogLevel.Info, $"Automatic update to {offer.Version}.");
            _ = InstallAsync();
        }
    }
}
