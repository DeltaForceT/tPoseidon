using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace tPoseidon.Panel;

public readonly record struct FlightFrame(double Scale, Point Centre, double Opacity);

public static class FlightMath
{
    public const double Seconds = 0.48;
    public const double EndShare = 0.5;
    public const double FadeStart = 0.55;

    public static double Ease(double t) => t < 0.5 ? 4 * t * t * t : 1 - Math.Pow(-2 * t + 2, 3) / 2;

    public static double EndScale(Size panel, double bubbleDiameter) => bubbleDiameter * EndShare / Math.Max(panel.Width, panel.Height);

    public static FlightFrame Sample(double progress, Point from, Point to, double endScale)
    {
        var p = Math.Clamp(progress, 0, 1);
        var e = Ease(p);
        var fade = p < FadeStart ? 1 : 1 - (p - FadeStart) / (1 - FadeStart);
        return new FlightFrame(1 + (endScale - 1) * e, new Point(from.X + (to.X - from.X) * e, from.Y + (to.Y - from.Y) * e), fade);
    }
}

public sealed class FlightWindow : Window
{
    private readonly Canvas _stage = new();
    private readonly Image _image = new() { Stretch = Stretch.Fill, IsHitTestVisible = false };
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _shift = new();
    private Point _origin;

    public FlightWindow(Bitmap picture, PixelRect box, PixelPoint panelOrigin, Size panelSize, double scale)
    {
        ArgumentNullException.ThrowIfNull(picture);
        SystemDecorations = SystemDecorations.None;
        Background = Brushes.Transparent;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        CanResize = false;
        IsHitTestVisible = false;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = box.Position;
        Width = box.Width / scale;
        Height = box.Height / scale;
        var left = (panelOrigin.X - box.X) / scale;
        var top = (panelOrigin.Y - box.Y) / scale;
        _origin = new Point(left + panelSize.Width / 2, top + panelSize.Height / 2);
        _image.Source = picture;
        _image.Width = panelSize.Width;
        _image.Height = panelSize.Height;
        _image.RenderTransformOrigin = RelativePoint.Center;
        _image.RenderTransform = new TransformGroup { Children = { _scale, _shift } };
        Canvas.SetLeft(_image, left);
        Canvas.SetTop(_image, top);
        _stage.Children.Add(_image);
        Content = _stage;
    }

    public Point Origin => _origin;

    public void Apply(FlightFrame frame)
    {
        _scale.ScaleX = frame.Scale;
        _scale.ScaleY = frame.Scale;
        _shift.X = frame.Centre.X - _origin.X;
        _shift.Y = frame.Centre.Y - _origin.Y;
        _image.Opacity = frame.Opacity;
    }
}

public sealed class PanelFlight
{
    private static readonly TimeSpan FrameStep = TimeSpan.FromMilliseconds(16);

    private readonly MainWindow _main;
    private readonly Func<BubbleWindow?> _current;
    private readonly Func<BubbleWindow?> _raise;
    private readonly Action _lower;
    private readonly Motion _motion;
    private bool _flying;

    public PanelFlight(MainWindow main, Func<BubbleWindow?> current, Func<BubbleWindow?> raise, Action lower, Motion motion)
    {
        _main = main ?? throw new ArgumentNullException(nameof(main));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _raise = raise ?? throw new ArgumentNullException(nameof(raise));
        _lower = lower ?? throw new ArgumentNullException(nameof(lower));
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
    }

    public bool IsFlying => _flying;

    public void Enter()
    {
        if (_flying || _main.WindowState != WindowState.Minimized || _raise() is not { } bubble)
        {
            return;
        }

        if (_motion.Reduced || _main.RestBox is not { } rest || Capture(rest.Size) is not { } picture)
        {
            return;
        }

        _flying = true;
        var overlay = Overlay(picture, rest.Origin, rest.Size, Scale(), bubble);
        overlay.Show();
        overlay.Apply(Frame(0, overlay, rest.Size, Scale(), bubble));
        Run(overlay, rest.Size, Scale(), bubble, false, bubble.Swallow);
    }

    public void Leave()
    {
        if (_flying || _current() is not { IsVisible: true } bubble)
        {
            return;
        }

        if (_motion.Reduced || _main.RestBox is null || !_main.Cloak(true))
        {
            _lower();
            return;
        }

        _flying = true;
        Dispatcher.UIThread.Post(() =>
        {
            var rest = _main.RestBox!.Value;
            if (Capture(rest.Size) is not { } picture)
            {
                Land(null);
                return;
            }

            var overlay = Overlay(picture, rest.Origin, rest.Size, Scale(), bubble);
            overlay.Show();
            overlay.Apply(Frame(1, overlay, rest.Size, Scale(), bubble));
            bubble.Swallow();
            Run(overlay, rest.Size, Scale(), bubble, true, null);
        }, DispatcherPriority.Loaded);
    }

    private double Scale() => _main.RenderScaling > 0 ? _main.RenderScaling : 1;

    private RenderTargetBitmap? Capture(Size size)
    {
        var scale = Scale();
        var pixels = new PixelSize((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale));
        if (pixels.Width <= 0 || pixels.Height <= 0)
        {
            return null;
        }

        var picture = new RenderTargetBitmap(pixels, new Vector(96 * scale, 96 * scale));
        picture.Render(_main);
        return picture;
    }

    private static FlightWindow Overlay(Bitmap picture, PixelPoint origin, Size size, double scale, BubbleWindow bubble)
    {
        var diameter = (int)Math.Ceiling(BubblePlacement.BodySize * scale);
        var half = diameter / 2;
        var margin = (int)Math.Ceiling(BubblePlacement.Pad * scale);
        var panel = new PixelRect(origin, new PixelSize((int)Math.Ceiling(size.Width * scale), (int)Math.Ceiling(size.Height * scale)));
        var body = new PixelRect(bubble.BodyCentre.X - half, bubble.BodyCentre.Y - half, diameter, diameter);
        var box = panel.Union(body);
        return new FlightWindow(picture, new PixelRect(box.X - margin, box.Y - margin, box.Width + margin * 2, box.Height + margin * 2), origin, size, scale);
    }

    private static FlightFrame Frame(double progress, FlightWindow overlay, Size size, double scale, BubbleWindow bubble)
    {
        var screen = overlay.Position;
        var target = new Point((bubble.BodyCentre.X - screen.X) / scale, (bubble.BodyCentre.Y - screen.Y) / scale);
        return FlightMath.Sample(progress, overlay.Origin, target, FlightMath.EndScale(size, BubblePlacement.BodySize));
    }

    private void Run(FlightWindow overlay, Size size, double scale, BubbleWindow bubble, bool reverse, Action? arrived)
    {
        var clock = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = FrameStep };
        timer.Tick += (_, _) =>
        {
            var t = Math.Clamp(clock.Elapsed.TotalSeconds / FlightMath.Seconds, 0, 1);
            overlay.Apply(Frame(reverse ? 1 - t : t, overlay, size, scale, bubble));
            if (t < 1)
            {
                return;
            }

            timer.Stop();
            arrived?.Invoke();
            Land(overlay);
        };
        timer.Start();
    }

    private void Land(FlightWindow? overlay)
    {
        overlay?.Close();
        _flying = false;
        if (_main.WindowState != WindowState.Minimized)
        {
            _main.Cloak(false);
            _lower();
            _main.Activate();
        }
    }
}
