using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace tPoseidon.Panel;

public sealed class BubbleButton : Border
{
    private const double GlyphSize = 22;
    private const double RingThickness = 2;
    private const double SpinnerSize = 40;
    private const double SpinnerThickness = 3;
    private const double SpinnerDash = 11;
    private const double SpinnerGap = 25;
    private const double MaxFrameSeconds = 0.05;
    private static readonly TimeSpan FrameStep = TimeSpan.FromMilliseconds(16);

    private readonly GlyphView _glyph;
    private readonly Border _glow = new() { Background = Brushes.White, Opacity = 0, IsHitTestVisible = false };
    private readonly Border _ring = new() { Opacity = 0, IsHitTestVisible = false, BorderBrush = Tokens.Success };
    private readonly Ellipse _spinner = new() { Width = SpinnerSize, Height = SpinnerSize, Stroke = Tokens.Accent, StrokeThickness = SpinnerThickness, StrokeDashArray = [SpinnerDash, SpinnerGap], StrokeLineCap = PenLineCap.Round, IsVisible = false, IsHitTestVisible = false };
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _shift = new();
    private readonly ScaleTransform _ringScale = new();
    private readonly RotateTransform _spin = new();
    private readonly ButtonFeel _feel = new();
    private readonly Motion _motion = Motion.Shared;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = FrameStep };
    private TimeSpan _last;
    private TimeSpan _confirmStart = TimeSpan.MinValue;
    private TimeSpan _shakeStart = TimeSpan.MinValue;
    private bool _busy;

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
        _glyph.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
        _glyph.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

        _glow.CornerRadius = CornerRadius;
        _ring.CornerRadius = CornerRadius;
        _ring.BorderThickness = new Thickness(RingThickness);
        _ring.RenderTransform = _ringScale;
        _spinner.RenderTransform = _spin;
        var layers = new Grid();
        layers.Children.Add(_glow);
        layers.Children.Add(_glyph);
        layers.Children.Add(_spinner);
        layers.Children.Add(_ring);
        Child = layers;
        RenderTransform = new TransformGroup { Children = [_scale, _shift] };
        ToolTip.SetTip(this, tool.Tip);

        _timer.Tick += (_, _) => Advance();
        PointerEntered += (_, _) => SetHovered(true);
        PointerExited += (_, _) => SetHovered(false);
        PointerPressed += OnPressed;
        PointerReleased += OnReleased;
        PointerCaptureLost += (_, _) => SetPressed(false);
        DetachedFromVisualTree += (_, _) => Halt();
        AttachedToVisualTree += (_, _) => Resume();
    }

    public event Action? Pressed;

    public BubbleTool Tool { get; }

    public bool IsBusy => _busy;

    public double PressScale => _feel.Scale;

    public double Glow => _feel.Glow;

    public bool IsTicking => _timer.IsEnabled;

    public void Press()
    {
        if (!_busy)
        {
            Pressed?.Invoke();
        }
    }

    public void BeginBusy()
    {
        _busy = true;
        ApplyLook();
        Wake();
    }

    public void EndBusy()
    {
        _busy = false;
        _spinner.IsVisible = false;
        ApplyLook();
    }

    public void Confirm()
    {
        if (_motion.Reduced)
        {
            return;
        }

        _confirmStart = _clock.Elapsed;
        Wake();
    }

    public void Fail()
    {
        if (_motion.Reduced)
        {
            return;
        }

        _shakeStart = _clock.Elapsed;
        BorderBrush = Tokens.Danger;
        Wake();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsEnabledProperty)
        {
            ApplyLook();
        }
    }

    private static IBrush ToneFor(BubbleTool tool) =>
        tool.Id switch
        {
            BubbleCatalog.StartAll => Tokens.Accent,
            BubbleCatalog.StopAll or BubbleCatalog.Exit => Tokens.Danger,
            _ => Tokens.TextPrimary,
        };

    private bool Interactive => IsEnabled && !_busy;

    private void SetHovered(bool on)
    {
        _feel.Hovered = on && Interactive;
        ApplyLook();
        Wake();
    }

    private void SetPressed(bool on)
    {
        _feel.Pressed = on && Interactive;
        Wake();
    }

    private void ApplyLook()
    {
        var lit = (_feel.Hovered || _feel.Pressed) && Interactive;
        Background = lit ? Tokens.Surface3 : Tokens.Surface2;
        if (_shakeStart == TimeSpan.MinValue)
        {
            BorderBrush = lit ? Tokens.Accent : Tokens.BorderSubtle;
        }

        Opacity = _busy ? ButtonEffects.BusyOpacity : IsEnabled ? 1 : ButtonEffects.DisabledOpacity;
        _spinner.IsVisible = _busy && !_motion.Reduced;
    }

    private void Wake()
    {
        if (_motion.Reduced)
        {
            _feel.Settle();
            Render();
            return;
        }

        if (!_timer.IsEnabled)
        {
            _last = _clock.Elapsed;
            _timer.Start();
        }
    }

    private void Halt()
    {
        _timer.Stop();
        _feel.Reset();
        _confirmStart = TimeSpan.MinValue;
        _shakeStart = TimeSpan.MinValue;
        _shift.X = 0;
        _ring.Opacity = 0;
        ApplyLook();
        Render();
    }

    private void Resume()
    {
        if (_busy)
        {
            Wake();
        }
    }

    private void Advance()
    {
        var now = _clock.Elapsed;
        _feel.Step(Math.Min((now - _last).TotalSeconds, MaxFrameSeconds));
        _last = now;
        Render();
        if (_feel.IsAtRest && !_busy && _confirmStart == TimeSpan.MinValue && _shakeStart == TimeSpan.MinValue)
        {
            _timer.Stop();
        }
    }

    private void Render()
    {
        _scale.ScaleX = _feel.Scale;
        _scale.ScaleY = _feel.Scale;
        _glow.Opacity = _feel.Glow;
        var now = _clock.Elapsed;
        RenderRing(now);
        RenderShake(now);
        if (_busy && !_motion.Reduced)
        {
            _spin.Angle = ButtonEffects.Spin(now.TotalSeconds);
        }
    }

    private void RenderRing(TimeSpan now)
    {
        if (_confirmStart == TimeSpan.MinValue)
        {
            return;
        }

        var elapsed = (now - _confirmStart).TotalSeconds;
        if (elapsed >= ButtonEffects.ConfirmLength)
        {
            _confirmStart = TimeSpan.MinValue;
            _ring.Opacity = 0;
            return;
        }

        var frame = ButtonEffects.Confirm(elapsed);
        _ringScale.ScaleX = frame.Scale;
        _ringScale.ScaleY = frame.Scale;
        _ring.Opacity = frame.Opacity;
    }

    private void RenderShake(TimeSpan now)
    {
        if (_shakeStart == TimeSpan.MinValue)
        {
            return;
        }

        var elapsed = (now - _shakeStart).TotalSeconds;
        if (elapsed >= ButtonEffects.ShakeLength)
        {
            _shakeStart = TimeSpan.MinValue;
            _shift.X = 0;
            ApplyLook();
            return;
        }

        _shift.X = ButtonEffects.Shake(elapsed);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            e.Pointer.Capture(this);
            SetPressed(true);
        }
    }

    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        var wasPressed = _feel.Pressed;
        e.Pointer.Capture(null);
        SetPressed(false);
        if (wasPressed && e.InitialPressMouseButton == MouseButton.Left && new Rect(Bounds.Size).Contains(e.GetPosition(this)))
        {
            Press();
        }
    }
}
