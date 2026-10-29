using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace tPoseidon.Panel;

public enum BubbleLayout
{
    Collapsed,
    Note,
    Open,
}

public sealed class BubbleWindow : Window
{
    public const double ButtonSize = 50;
    public const double ButtonGap = 8;
    public const double CardPadding = 14;
    public const double GroupGap = 12;
    public const double CaptionHeight = 18;
    public const double NoteWidth = 290;
    public const double NoteHeight = 64;
    public const double EmblemSize = 56;

    private const double RingThickness = 4;
    private const double DragThreshold = 4;
    private const double CollapsedScale = 0.72;
    private const double RiseDistance = 10;
    private const double StaggerShare = 0.1;
    private const double PulseGrowth = 0.5;
    private const double PulseStrength = 0.65;
    private static readonly TimeSpan FrameStep = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan ExpandLength = TimeSpan.FromMilliseconds(240);
    private static readonly TimeSpan CollapseLength = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan PulsePeriod = TimeSpan.FromMilliseconds(1400);
    private static readonly TimeSpan AlertLength = TimeSpan.FromSeconds(12);

    private readonly PanelHost _host;
    private readonly PanelPreferences _preferences;
    private readonly IReadOnlyList<BubbleGroup> _groups;
    private readonly Action _openLogs;
    private readonly Grid _stage = new();
    private readonly Border _body = new();
    private readonly BubbleFace _face = new(Motion.Shared);
    private readonly ScaleTransform _emblemScale = new();
    private readonly Border _pulse = new() { IsHitTestVisible = false, Opacity = 0 };
    private readonly Border _card = new();
    private readonly ScaleTransform _cardScale = new();
    private readonly Border _note = new();
    private readonly TextBlock _noteTitle = new() { FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly TextBlock _noteDetail = new() { FontSize = Tokens.FontSizeCaption, Foreground = Tokens.TextSecondary, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly List<(StackPanel Row, TranslateTransform Shift)> _rows = [];
    private readonly List<(BubbleTool Tool, BubbleButton Button)> _buttons = [];
    private readonly ScaleTransform _pulseScale = new();
    private readonly Motion _motion = Motion.Shared;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private DispatcherTimer? _tween;
    private DispatcherTimer? _pulseTimer;
    private IDisposable? _noteTimer;
    private IDisposable? _alertTimer;
    private PixelPoint _anchor;
    private PixelPoint _dragLast;
    private bool _pressed;
    private bool _dragging;
    private bool _onRight = true;
    private bool _onBottom = true;
    private bool _expanded;
    private BubbleLayout _layout = BubbleLayout.Collapsed;
    private bool _alerting;
    private bool _noteLogs;
    private bool _retired;
    private BubbleState _state = BubbleState.Stopped;

    public BubbleWindow(PanelHost host, PanelPreferences preferences, IReadOnlyList<BubbleGroup> groups, Action openLogs)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _openLogs = openLogs ?? throw new ArgumentNullException(nameof(openLogs));
        Title = "tPoseidon quick access";
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        FontFamily = Tokens.UiFont;
        FontSize = Tokens.FontSizeBody;
        Foreground = Tokens.TextPrimary;
        UseLayoutRounding = true;
        Icon = PanelAssets.WindowIcon();
        WindowStartupLocation = WindowStartupLocation.Manual;

        BuildBody();
        BuildCard();
        BuildNote();
        Content = _stage;
        ApplyLayout();
        Refresh();

        _face.Scaled += OnFaceScaled;
        _host.ServicesRebuilt += Refresh;
        _host.ServiceChanged += OnServiceChanged;
        _host.Notice += OnNotice;
        _motion.Changed += OnMotionChanged;
        Closing += OnClosingRequested;
        Closed += OnWindowClosed;
    }

    public bool IsExpanded => _expanded;

    public bool IsAlerting => _alerting;

    public BubbleState State => _state;

    public PixelPoint Anchor => _anchor;

    public bool IsNoteShown => _layout == BubbleLayout.Note;

    public BubbleLayout Layout => _layout;

    public Size CurrentSize => _layout switch
    {
        BubbleLayout.Open => ExpandedSize,
        BubbleLayout.Note => NoteSize,
        _ => CollapsedSize,
    };

    public BubbleFace Face => _face;

    public IReadOnlyList<BubbleGroup> Groups => _groups;

    public IReadOnlyList<(BubbleTool Tool, BubbleButton Button)> Buttons => _buttons;

    public string Summary => (string)ToolTip.GetTip(_body)!;

    public PixelPoint BodyCentre
    {
        get
        {
            var half = BodyPixels() / 2;
            return new PixelPoint(_anchor.X + half, _anchor.Y + half);
        }
    }

    public void Retire()
    {
        _retired = true;
        Close();
    }

    public void PlaceAtSavedPosition(PixelRect? preferredArea = null)
    {
        var areas = WorkAreas();
        var size = BodyPixels();
        var home = preferredArea ?? (areas.Count > 0 ? areas[0] : null);
        var desired = _preferences.SavedBubble
            ?? (home is { } area ? BubblePlacement.Default(area, size) : new PixelPoint(0, 0));
        _anchor = BubblePlacement.Clamp(desired, size, areas);
        ApplyLayout();
    }

    public void ToggleExpanded()
    {
        if (_expanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    public void Expand()
    {
        if (_expanded)
        {
            return;
        }

        _expanded = true;
        HideNote();
        StopAlert();
        UpdateEnabled();
        ChooseSide();
        _layout = BubbleLayout.Open;
        ApplyLayout();
        Run(ExpandLength, 0, 1, ApplyOpening, null);
    }

    public void Collapse()
    {
        if (!_expanded)
        {
            return;
        }

        _expanded = false;
        Run(CollapseLength, 1, 0, ApplyOpening, () =>
        {
            _layout = BubbleLayout.Collapsed;
            ApplyLayout();
            ApplyOpening(0);
        });
    }

    public void Swallow()
    {
        _face.Gulp();
    }

    public void CollapseNow()
    {
        _tween?.Stop();
        _expanded = false;
        HideNote();
        StopAlert();
        _layout = BubbleLayout.Collapsed;
        ApplyLayout();
        ApplyOpening(0);
    }

    public void Alert(string title, string? detail, bool openLogsOnClick)
    {
        if (_layout == BubbleLayout.Open)
        {
            return;
        }

        _alerting = openLogsOnClick;
        _noteLogs = openLogsOnClick;
        _noteTitle.Text = title;
        _noteDetail.Text = detail ?? "";
        _noteDetail.IsVisible = !string.IsNullOrEmpty(detail);
        _noteTitle.Foreground = openLogsOnClick ? Tokens.Danger : Tokens.TextPrimary;
        ChooseSide();
        _note.IsVisible = true;
        _layout = BubbleLayout.Note;
        ApplyLayout();
        _noteTimer?.Dispose();
        _noteTimer = DispatcherTimer.RunOnce(HideNote, AlertLength);
        if (openLogsOnClick)
        {
            StartPulse();
            _alertTimer?.Dispose();
            _alertTimer = DispatcherTimer.RunOnce(StopAlert, AlertLength);
        }
    }

    public void ClickNote()
    {
        var opensLogs = _noteLogs;
        HideNote();
        StopAlert();
        if (opensLogs)
        {
            _openLogs();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty)
        {
            _face.SetRunning(IsVisible);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Collapse();
        }
    }

    private void OnClosingRequested(object? sender, WindowClosingEventArgs e)
    {
        if (!_retired && e.CloseReason == WindowCloseReason.WindowClosing)
        {
            e.Cancel = true;
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _host.ServicesRebuilt -= Refresh;
        _host.ServiceChanged -= OnServiceChanged;
        _host.Notice -= OnNotice;
        _motion.Changed -= OnMotionChanged;
        _face.Scaled -= OnFaceScaled;
        _face.SetRunning(false);
        _tween?.Stop();
        _pulseTimer?.Stop();
        _noteTimer?.Dispose();
        _alertTimer?.Dispose();
    }

    private void OnServiceChanged(ServiceModel service, ServiceTransition transition) => Refresh();

    private void OnNotice(PanelNotice notice)
    {
        if (!IsVisible)
        {
            return;
        }

        Alert(notice.Title, notice.Detail, notice.Kind == BannerKind.Error);
    }

    private void OnFaceScaled(double scale)
    {
        _emblemScale.ScaleX = scale;
        _emblemScale.ScaleY = scale;
    }

    private void OnMotionChanged()
    {
        _face.SetRunning(IsVisible);
        if (_alerting)
        {
            StartPulse();
        }
    }

    private void Refresh()
    {
        _state = BubbleStatus.From(_host.Services);
        var brush = _state switch
        {
            BubbleState.Running => Tokens.Success,
            BubbleState.Partial => Tokens.Warning,
            BubbleState.Failed => Tokens.Danger,
            _ => Tokens.TextMuted,
        };
        _face.SetStatus(((ISolidColorBrush)brush).Color, _state != BubbleState.Stopped);
        ToolTip.SetTip(_body, _host.IsSetUp ? BubbleStatus.Describe(_state, _host.Services) : "Not set up yet");
        UpdateEnabled();
    }

    private void UpdateEnabled()
    {
        foreach (var (tool, button) in _buttons)
        {
            button.IsEnabled = tool.Enabled();
        }
    }

    private void BuildBody()
    {
        var emblem = new Image
        {
            Source = new Bitmap(AssetLoader.Open(PanelAssets.Uri("emblem-48.png"))),
            Width = EmblemSize,
            Height = EmblemSize,
            IsHitTestVisible = false,
            RenderTransform = _emblemScale,
        };
        _face.Width = BubblePlacement.CellSize;
        _face.Height = BubblePlacement.CellSize;
        var layers = new Grid { Width = BubblePlacement.BodySize, Height = BubblePlacement.BodySize };
        layers.Children.Add(_face);
        layers.Children.Add(emblem);
        _body.Width = BubblePlacement.BodySize;
        _body.Height = BubblePlacement.BodySize;
        _body.Background = Brushes.Transparent;
        _body.Child = layers;
        _body.Cursor = new Cursor(StandardCursorType.Hand);
        _body.PointerPressed += OnBodyPressed;
        _body.PointerMoved += OnBodyMoved;
        _body.PointerReleased += OnBodyReleased;

        _pulse.Width = BubblePlacement.BodySize;
        _pulse.Height = BubblePlacement.BodySize;
        _pulse.CornerRadius = new CornerRadius(BubblePlacement.BodySize / 2);
        _pulse.BorderThickness = new Thickness(RingThickness);
        _pulse.BorderBrush = Tokens.Danger;
        _pulse.RenderTransform = _pulseScale;
    }

    private void BuildCard()
    {
        var stack = new StackPanel { Spacing = GroupGap };
        foreach (var group in _groups)
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock { Text = group.Name, Height = CaptionHeight, FontSize = Tokens.FontSizeCaption, FontWeight = FontWeight.SemiBold, Foreground = Tokens.TextMuted });
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = ButtonGap };
            foreach (var tool in group.Tools)
            {
                var button = new BubbleButton(tool);
                button.Pressed += async () => await RunToolAsync(tool, button).ConfigureAwait(true);
                _buttons.Add((tool, button));
                buttons.Children.Add(button);
            }

            row.Children.Add(buttons);
            var shift = new TranslateTransform();
            row.RenderTransform = shift;
            _rows.Add((row, shift));
            stack.Children.Add(row);
        }

        _card.Child = stack;
        _card.Padding = new Thickness(CardPadding);
        _card.Background = Tokens.Surface1;
        _card.BorderBrush = Tokens.BorderSubtle;
        _card.BorderThickness = new Thickness(1);
        _card.CornerRadius = new CornerRadius(16);
        _card.BoxShadow = new BoxShadows(new BoxShadow { Blur = 20, OffsetY = 6, Color = Color.FromArgb(0x80, 0, 0, 0) });
        _card.RenderTransform = _cardScale;
        _card.Opacity = 0;
        _card.IsVisible = false;
        _card.Width = CardWidth;
        _card.Height = CardHeight;
    }

    private void BuildNote()
    {
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 1 };
        text.Children.Add(_noteTitle);
        text.Children.Add(_noteDetail);
        _note.Child = text;
        _note.Width = NoteWidth;
        _note.Height = NoteHeight;
        _note.Padding = new Thickness(12, 0);
        _note.Background = Tokens.Surface1;
        _note.BorderBrush = Tokens.BorderSubtle;
        _note.BorderThickness = new Thickness(1);
        _note.CornerRadius = new CornerRadius(12);
        _note.BoxShadow = new BoxShadows(new BoxShadow { Blur = 16, OffsetY = 4, Color = Color.FromArgb(0x70, 0, 0, 0) });
        _note.Cursor = new Cursor(StandardCursorType.Hand);
        _note.IsVisible = false;
        _note.PointerReleased += (_, _) => ClickNote();
    }

    private double CardWidth
    {
        get
        {
            var widest = _groups.Max(group => group.Tools.Count);
            return CardPadding * 2 + widest * ButtonSize + (widest - 1) * ButtonGap;
        }
    }

    private double CardHeight =>
        CardPadding * 2 + _groups.Count * (CaptionHeight + 4 + ButtonSize) + (_groups.Count - 1) * GroupGap;

    public Size ExpandedSize => new(CardWidth, BubblePlacement.CellSize + CardHeight);

    public static Size NoteSize => new(BubblePlacement.CellSize + NoteWidth, BubblePlacement.CellSize);

    public static Size CollapsedSize => new(BubblePlacement.CellSize, BubblePlacement.CellSize);

    private void ApplyLayout()
    {
        var size = CurrentSize;
        Width = size.Width;
        Height = size.Height;
        _stage.Children.Clear();
        _stage.Width = size.Width;
        _stage.Height = size.Height;
        var bodyHorizontal = _onRight ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        var bodyVertical = _onBottom ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _body.HorizontalAlignment = bodyHorizontal;
        _body.VerticalAlignment = bodyVertical;
        _body.Margin = new Thickness(BubblePlacement.Pad);
        _pulse.HorizontalAlignment = bodyHorizontal;
        _pulse.VerticalAlignment = bodyVertical;
        _pulse.Margin = new Thickness(BubblePlacement.Pad);

        if (_layout == BubbleLayout.Open)
        {
            _card.HorizontalAlignment = bodyHorizontal;
            _card.VerticalAlignment = _onBottom ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            _card.RenderTransformOrigin = new RelativePoint(_onRight ? 0.92 : 0.08, _onBottom ? 1 : 0, RelativeUnit.Relative);
            _card.IsVisible = true;
            _stage.Children.Add(_card);
        }
        else
        {
            _card.IsVisible = false;
            _card.Opacity = 0;
        }

        if (_layout == BubbleLayout.Note)
        {
            _note.HorizontalAlignment = _onRight ? HorizontalAlignment.Left : HorizontalAlignment.Right;
            _note.VerticalAlignment = VerticalAlignment.Center;
            _note.Margin = new Thickness(_onRight ? BubblePlacement.Pad : 0, 0, _onRight ? 0 : BubblePlacement.Pad, 0);
            _stage.Children.Add(_note);
        }

        _stage.Children.Add(_pulse);
        _stage.Children.Add(_body);
        Position = BubblePlacement.WindowPosition(_anchor, Scale, size, _onRight, _onBottom);
    }

    private void ChooseSide()
    {
        var areas = WorkAreas();
        if (areas.Count == 0)
        {
            return;
        }

        var size = BodyPixels();
        var center = new PixelPoint(_anchor.X + size / 2, _anchor.Y + size / 2);
        var area = areas.Where(candidate => candidate.Contains(center)).Cast<PixelRect?>().FirstOrDefault() ?? areas[0];
        (_onRight, _onBottom) = BubblePlacement.Quadrant(_anchor, size, area);
    }

    private double Scale => RenderScaling > 0 ? RenderScaling : 1;

    private int BodyPixels() => (int)Math.Round(BubblePlacement.BodySize * Scale);

    private List<PixelRect> WorkAreas() => Screens.All.Select(screen => screen.WorkingArea).ToList();

    private void ApplyOpening(double progress)
    {
        var eased = Ease(progress);
        _card.Opacity = eased;
        var scale = CollapsedScale + (1 - CollapsedScale) * eased;
        _cardScale.ScaleX = scale;
        _cardScale.ScaleY = scale;
        var rise = _onBottom ? RiseDistance : -RiseDistance;
        for (var i = 0; i < _rows.Count; i++)
        {
            var local = Ease(Math.Clamp((progress - i * StaggerShare) / (1 - (_rows.Count - 1) * StaggerShare), 0, 1));
            _rows[i].Row.Opacity = local;
            _rows[i].Shift.Y = rise * (1 - local);
        }
    }

    private static double Ease(double t) => 1 - Math.Pow(1 - t, 3);

    private void Run(TimeSpan length, double from, double to, Action<double> step, Action? done)
    {
        _tween?.Stop();
        if (_motion.Reduced)
        {
            step(to);
            done?.Invoke();
            return;
        }

        var started = _clock.Elapsed;
        var timer = new DispatcherTimer { Interval = FrameStep };
        _tween = timer;
        timer.Tick += (_, _) =>
        {
            var t = Math.Clamp((_clock.Elapsed - started) / length, 0, 1);
            step(from + (to - from) * t);
            if (t < 1)
            {
                return;
            }

            timer.Stop();
            done?.Invoke();
        };
        step(from);
        timer.Start();
    }

    private void StartPulse()
    {
        _pulseTimer?.Stop();
        if (_motion.Reduced)
        {
            _pulse.Opacity = PulseStrength;
            _pulseScale.ScaleX = 1;
            _pulseScale.ScaleY = 1;
            return;
        }

        var started = _clock.Elapsed;
        var timer = new DispatcherTimer { Interval = FrameStep };
        _pulseTimer = timer;
        timer.Tick += (_, _) =>
        {
            var t = (_clock.Elapsed - started).TotalMilliseconds % PulsePeriod.TotalMilliseconds / PulsePeriod.TotalMilliseconds;
            var scale = 1 + PulseGrowth * Ease(t);
            _pulseScale.ScaleX = scale;
            _pulseScale.ScaleY = scale;
            _pulse.Opacity = PulseStrength * (1 - Ease(t));
        };
        timer.Start();
    }

    private void StopAlert()
    {
        _alerting = false;
        _alertTimer?.Dispose();
        _pulseTimer?.Stop();
        _pulse.Opacity = 0;
    }

    private void HideNote()
    {
        _noteTimer?.Dispose();
        if (!_note.IsVisible)
        {
            return;
        }

        _note.IsVisible = false;
        if (_layout == BubbleLayout.Note)
        {
            _layout = BubbleLayout.Collapsed;
            ApplyLayout();
        }
    }

    private async Task RunToolAsync(BubbleTool tool, BubbleButton button)
    {
        if (tool.ClosesBubble)
        {
            Collapse();
        }

        await tool.Run(button).ConfigureAwait(true);
    }

    private void OnBodyPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(_body).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = true;
        _dragging = false;
        _dragLast = this.PointToScreen(e.GetPosition(this));
        e.Pointer.Capture(_body);
    }

    private void OnBodyMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        var current = this.PointToScreen(e.GetPosition(this));
        var dx = current.X - _dragLast.X;
        var dy = current.Y - _dragLast.Y;
        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < DragThreshold * Scale)
        {
            return;
        }

        _dragging = true;
        _anchor = new PixelPoint(_anchor.X + dx, _anchor.Y + dy);
        _dragLast = current;
        Position = BubblePlacement.WindowPosition(_anchor, Scale, CurrentSize, _onRight, _onBottom);
    }

    private void OnBodyReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        e.Pointer.Capture(null);
        if (_dragging)
        {
            _dragging = false;
            Settle();
            return;
        }

        ToggleExpanded();
    }

    private void Settle()
    {
        _anchor = BubblePlacement.Clamp(_anchor, BodyPixels(), WorkAreas());
        ApplyLayout();
        _preferences.RememberBubble(_anchor);
        _preferences.TrySave();
    }
}

public sealed class BubbleButton : Border
{
    private const double GlyphSize = 22;

    private readonly GlyphView _glyph;

    public BubbleButton(BubbleTool tool)
    {
        Tool = tool ?? throw new ArgumentNullException(nameof(tool));
        Width = BubbleWindow.ButtonSize;
        Height = BubbleWindow.ButtonSize;
        CornerRadius = new CornerRadius(BubbleWindow.ButtonSize / 2);
        Background = Tokens.Surface2;
        BorderBrush = Tokens.BorderSubtle;
        BorderThickness = new Thickness(1);
        Cursor = new Cursor(StandardCursorType.Hand);
        _glyph = Tokens.Glyphs(tool.Glyph, GlyphSize, ToneFor(tool));
        Child = _glyph;
        ToolTip.SetTip(this, tool.Tip);
        PointerEntered += (_, _) => Highlight(true);
        PointerExited += (_, _) => Highlight(false);
        PointerReleased += OnReleased;
    }

    public event Action? Pressed;

    public BubbleTool Tool { get; }

    public void Press() => Pressed?.Invoke();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            Opacity = IsEnabled ? 1 : 0.4;
        }
    }

    private static IBrush ToneFor(BubbleTool tool) =>
        tool.Id switch
        {
            BubbleCatalog.StartAll => Tokens.Accent,
            BubbleCatalog.StopAll or BubbleCatalog.Exit => Tokens.Danger,
            _ => Tokens.TextPrimary,
        };

    private void Highlight(bool on)
    {
        Background = on && IsEnabled ? Tokens.Surface3 : Tokens.Surface2;
        BorderBrush = on && IsEnabled ? Tokens.Accent : Tokens.BorderSubtle;
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (IsEnabled && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Press();
        }
    }
}
