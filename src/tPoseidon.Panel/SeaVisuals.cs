using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Skia;
using SkiaSharp;

namespace tPoseidon.Panel;

public static class SeaPalette
{
    public static readonly SKColor Navy = new(0x0B, 0x2A, 0x5B);
    public static readonly SKColor Deep = new(0x0D, 0x47, 0xA1);
    public static readonly SKColor Blue = new(0x1E, 0x7F, 0xD8);
    public static readonly SKColor Sky = new(0x2F, 0xA8, 0xE6);
    public static readonly SKColor Aqua = new(0x5C, 0xD6, 0xEE);
    public static readonly SKColor Light = new(0xA8, 0xF0, 0xFA);
    public static readonly SKColor Foam = new(0xF2, 0xFD, 0xFF);
    public static readonly SKColor Abyss = new(0x07, 0x1A, 0x3A);
    public static readonly SKColor Content = new(0x0A, 0x25, 0x54);
    public static readonly SKColor Coral = new(0xFF, 0x6F, 0x61);
    public static readonly SKColor Peach = new(0xFF, 0xD9, 0xB8);
    public static readonly SKColor Shell = new(0xF6, 0xB2, 0x8E);
    public static readonly SKColor Fish = new(0xFF, 0x9A, 0x5C);
    public static readonly SKColor Kelp = new(0x1F, 0x9E, 0x8A);
    public static readonly SKColor Sand = new(0x1C, 0x42, 0x78);
}

public sealed class SeaHost : Control
{
    private readonly SeaHandler _handler;
    private readonly Motion _motion;
    private CompositionCustomVisual? _visual;
    private object? _pending;

    public SeaHost(SeaHandler handler, Motion motion)
    {
        _handler = handler;
        _motion = motion;
        ClipToBounds = true;
        IsHitTestVisible = false;
    }

    public void Send(object message)
    {
        if (_visual is null)
            _pending = message;
        else
            _visual.SendHandlerMessage(message);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var compositor = ElementComposition.GetElementVisual(this)?.Compositor;
        if (compositor is null)
            return;
        _visual = compositor.CreateCustomVisual(_handler);
        _visual.Size = new Vector(Bounds.Width, Bounds.Height);
        ElementComposition.SetElementChildVisual(this, _visual);
        _motion.Changed += PushMotion;
        PushMotion();
        if (_pending is not null)
        {
            _visual.SendHandlerMessage(_pending);
            _pending = null;
        }
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _motion.Changed -= PushMotion;
        ElementComposition.SetElementChildVisual(this, null);
        _visual = null;
        base.OnUnloaded(e);
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        if (_visual is not null)
            _visual.Size = new Vector(e.NewSize.Width, e.NewSize.Height);
    }

    private void PushMotion() => _visual?.SendHandlerMessage(_motion.State);
}

public abstract class SeaHandler : CompositionCustomVisualHandler
{
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(30);
    private TimeSpan _lastTick;
    private TimeSpan _lastDraw;
    private bool _registered;

    protected double T { get; private set; }

    public static bool GpuBacked { get; private set; }

    protected bool AmbientOn { get; private set; }

    protected bool EventsOn { get; private set; } = true;

    public override void OnMessage(object message)
    {
        if (message is MotionState state)
        {
            AmbientOn = state.Ambient;
            EventsOn = state.Events;
            Kick();
            Invalidate();
            return;
        }
        OnCustomMessage(message);
        Kick();
        Invalidate();
    }

    protected virtual void OnCustomMessage(object message)
    {
    }

    protected virtual bool HasTransient(TimeSpan now) => false;

    protected virtual bool WantsAmbientFrames => true;

    protected TimeSpan Now => CompositionNow;

    private void Kick()
    {
        if (_registered)
            return;
        _registered = true;
        _lastTick = CompositionNow;
        RegisterForNextAnimationFrameUpdate();
    }

    public override void OnAnimationFrameUpdate()
    {
        _registered = false;
        var now = CompositionNow;
        var dt = now - _lastTick;
        _lastTick = now;
        if (AmbientOn)
            T += dt.TotalSeconds;
        if (!(AmbientOn && WantsAmbientFrames) && !HasTransient(now))
        {
            Invalidate();
            return;
        }
        if (now - _lastDraw >= FrameInterval)
        {
            _lastDraw = now;
            InvalidateFrame();
        }
        _registered = true;
        RegisterForNextAnimationFrameUpdate();
    }

    protected virtual void InvalidateFrame() => Invalidate();

    public override void OnRender(ImmediateDrawingContext drawingContext)
    {
        if (drawingContext.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature)
            return;
        using var lease = feature.Lease();
        GpuBacked = lease.GrContext is not null;
        var size = EffectiveSize;
        var canvas = lease.SkCanvas;
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, (float)size.X, (float)size.Y));
        Draw(canvas, (float)size.X, (float)size.Y);
        canvas.Restore();
    }

    protected abstract void Draw(SKCanvas canvas, float width, float height);

    protected static SKPath WavePath(float width, float baseline, float amplitude, float wavelength, float bottom)
    {
        var path = new SKPath();
        path.MoveTo(0, baseline);
        for (float x = 0; x < width + wavelength; x += wavelength)
        {
            path.QuadTo(x + wavelength / 4, baseline - amplitude, x + wavelength / 2, baseline);
            path.QuadTo(x + wavelength * 3 / 4, baseline + amplitude, x + wavelength, baseline);
        }
        var end = path.LastPoint.X;
        path.LineTo(end, bottom);
        path.LineTo(0, bottom);
        path.Close();
        return path;
    }

    protected static SKPaint Fill(SKColor color) => new() { IsAntialias = true, Color = color, Style = SKPaintStyle.Fill };

    protected static SKPaint Stroke(SKColor color, float width) =>
        new() { IsAntialias = true, Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = width, StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round };
}

public enum ServerStatus
{
    Stopped,
    Starting,
    Running,
    Crashed,
}

public sealed record BadgeState(ServerStatus Status, bool Ripple, bool Shake);
