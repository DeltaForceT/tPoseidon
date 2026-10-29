using Avalonia;
using Avalonia.Controls;

namespace tPoseidon.Panel;

public sealed class BubbleShell
{
    private readonly MainWindow _main;
    private readonly PanelHost _host;
    private readonly PanelPreferences _preferences;
    private BubbleWindow? _bubble;
    private bool _ready;
    private bool _retired;

    public BubbleShell(MainWindow main, PanelHost host, PanelPreferences preferences)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _main.Flight = new PanelFlight(_main, () => _bubble, Raise, Lower, Motion.Shared);
        _main.BubbleModeChanged += Refresh;
    }

    public BubbleWindow? Bubble => _bubble;

    public void Start()
    {
        _ready = true;
        Refresh();
    }

    public void Refresh()
    {
        if (!_ready || _retired)
        {
            return;
        }

        if (BubblePlacement.ShouldShow(_preferences.BubbleEnabled, _main.WindowState == WindowState.Minimized, _main.SetupOpen))
        {
            Raise();
        }
        else
        {
            Lower();
        }
    }

    public void Retire()
    {
        _retired = true;
        _bubble?.Retire();
        _bubble = null;
    }

    private BubbleWindow? Raise()
    {
        if (!_ready || _retired || !BubblePlacement.ShouldShow(_preferences.BubbleEnabled, true, _main.SetupOpen))
        {
            return null;
        }

        _bubble ??= new BubbleWindow(_host, _preferences, BubbleCatalog.Build(_main.Tools, _main.Reveal, _main.Exit), PanelTools.OpenLogs);
        if (_bubble.IsVisible)
        {
            return _bubble;
        }

        _bubble.Show();
        var home = _main.RestBox is { } rest ? _main.Screens.ScreenFromPoint(rest.Origin)?.WorkingArea : null;
        _bubble.PlaceAtSavedPosition(home);
        return _bubble;
    }

    private void Lower()
    {
        if (_bubble is { IsVisible: true } visible)
        {
            visible.CollapseNow();
            visible.Hide();
        }
    }
}
