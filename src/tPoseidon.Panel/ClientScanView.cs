using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using tPoseidon.Hosting.ClientScan;

namespace tPoseidon.Panel;

public sealed class ClientScanView : StackPanel
{
    private const double RowSpacing = 6;
    private const double ButtonHeight = 28;

    private readonly ClientScanModel _model;
    private readonly Banner _summary = new();
    private readonly Banner _notice = new();
    private readonly StackPanel _general = new() { Spacing = RowSpacing };
    private readonly StackPanel _maps = new() { Spacing = RowSpacing };
    private readonly Button _fixAll = Action("Fix", "primary");
    private readonly Button _restore = Action("Restore originals", "secondary");
    private readonly Button _scanAgain = Action("Scan again", "secondary");
    private readonly TextBlock _busy = new() { Text = "Scanning the client...", Foreground = Tokens.TextSecondary, FontSize = Tokens.FontSizeCaption };

    public ClientScanView(ClientScanModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Spacing = 10;
        IsVisible = false;
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        buttons.Children.Add(_fixAll);
        buttons.Children.Add(_restore);
        buttons.Children.Add(_scanAgain);
        Children.Add(_busy);
        Children.Add(_summary);
        Children.Add(_notice);
        Children.Add(_general);
        Children.Add(_maps);
        Children.Add(buttons);
        _fixAll.Click += async (_, _) => await _model.FixAllAsync().ConfigureAwait(true);
        _restore.Click += async (_, _) => await _model.RestoreAsync().ConfigureAwait(true);
        _scanAgain.Click += async (_, _) => await ScanAgainAsync().ConfigureAwait(true);
        _model.Changed += Render;
    }

    public Func<ClientScanContext?>? ContextSource { get; set; }

    public Button FixAllButton => _fixAll;

    public Button RestoreButton => _restore;

    public Task ScanAsync(ClientScanContext context)
    {
        IsVisible = true;
        return _model.ScanAsync(context);
    }

    public void Render()
    {
        IsVisible = true;
        _busy.IsVisible = _model.Busy;
        _fixAll.IsEnabled = !_model.Busy;
        _restore.IsEnabled = !_model.Busy;
        _scanAgain.IsEnabled = !_model.Busy;
        _fixAll.IsVisible = _model.HasFixes;
        _restore.IsVisible = _model.CanRestore;
        RenderNotice();
        _general.Children.Clear();
        _maps.Children.Clear();
        if (_model.Report is not { } report)
        {
            _summary.Hide();
            return;
        }

        _summary.Show(Kind(report), report.Blocking ? "The client has problems." : report.Warnings > 0 ? "The client works, with warnings." : "The client has what the emulator needs.", report.Summary);
        foreach (var finding in report.General)
        {
            _general.Children.Add(Row(finding, report));
        }

        foreach (var map in report.ByGroup)
        {
            _maps.Children.Add(MapGroup(map.Key, map.ToList(), report));
        }
    }

    private async Task ScanAgainAsync()
    {
        if (ContextSource?.Invoke() is { } context)
        {
            await _model.ScanAsync(context).ConfigureAwait(true);
        }
    }

    private void RenderNotice()
    {
        if (_model.Notice is { } notice)
        {
            _notice.Show(notice.Kind, notice.Title, notice.Detail);
        }
        else
        {
            _notice.Hide();
        }
    }

    private Expander MapGroup(string title, IReadOnlyList<ScanFinding> findings, ScanReport report)
    {
        var rows = new StackPanel { Spacing = RowSpacing, Margin = new Thickness(0, 4, 0, 4) };
        foreach (var finding in findings)
        {
            rows.Children.Add(Row(finding, report));
        }

        var worst = findings.Any(finding => finding.Status == ScanStatus.Problem) ? ScanStatus.Problem : findings.Any(finding => finding.Status == ScanStatus.Warning) ? ScanStatus.Warning : ScanStatus.Info;
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(StatusGlyph(worst));
        header.Children.Add(new TextBlock { Text = $"{title} ({findings.Count})", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        return new Expander { Header = header, Content = rows, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    }

    private DockPanel Row(ScanFinding finding, ScanReport report)
    {
        var dock = new DockPanel { LastChildFill = true };
        var icon = StatusGlyph(finding.Status);
        icon.VerticalAlignment = VerticalAlignment.Top;
        icon.Margin = new Thickness(0, 2, 10, 0);
        DockPanel.SetDock(icon, Dock.Left);
        dock.Children.Add(icon);
        if (finding.FixId is { } fixId && finding.Status != ScanStatus.Pass && report.FixTitles.ContainsKey(fixId))
        {
            var fix = Action("Fix", "secondary");
            fix.Margin = new Thickness(8, 0, 0, 0);
            fix.VerticalAlignment = VerticalAlignment.Top;
            fix.IsEnabled = !_model.Busy;
            Avalonia.Controls.ToolTip.SetTip(fix, _model.FixTitle(fixId));
            fix.Click += async (_, _) => await _model.FixAsync([fixId]).ConfigureAwait(true);
            DockPanel.SetDock(fix, Dock.Right);
            dock.Children.Add(fix);
        }

        dock.Children.Add(new TextBlock { Text = finding.Message, TextWrapping = TextWrapping.Wrap, FontSize = Tokens.FontSizeCaption, Foreground = finding.Status == ScanStatus.Pass ? Tokens.TextSecondary : Tokens.TextPrimary });
        return dock;
    }

    private static GlyphView StatusGlyph(ScanStatus status) => status switch
    {
        ScanStatus.Pass => Tokens.Glyphs(Glyph.Check, 12, Tokens.Success),
        ScanStatus.Info => Tokens.Glyphs(Glyph.Info, 12, Tokens.Accent),
        ScanStatus.Warning => Tokens.Glyphs(Glyph.Warn, 12, Tokens.Warning),
        _ => Tokens.Glyphs(Glyph.Error, 12, Tokens.Danger),
    };

    private static BannerKind Kind(ScanReport report) =>
        report.Blocking ? BannerKind.Error : report.Warnings > 0 ? BannerKind.Warning : BannerKind.Success;

    private static Button Action(string text, string skin)
    {
        var button = new Button { Content = text, Height = ButtonHeight, MinHeight = 0, Padding = new Thickness(14, 0), VerticalContentAlignment = VerticalAlignment.Center };
        button.Classes.Add(skin);
        return button;
    }
}
