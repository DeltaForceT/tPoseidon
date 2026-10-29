using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace tPoseidon.Panel;

public readonly record struct BubblePose(double Appear, double ScaleX, double ScaleY, double Flash, double Glint, double Clock);

public static class BubbleMotion
{
    public const double IdleRate = 0.35;
    public const double GlintPeriod = 5.4;
    public const double GlintLength = 0.8;
    public const double AppearLength = 0.3;
    public const double GulpLength = 0.6;
    public const double FlashLength = 0.4;
    public const double GulpStretch = 0.17;
    public const double GulpCycles = 2.5;
    public const double SquashShare = 0.85;
    public const double Overshoot = 1.70158;

    public static BubblePose Sample(double now, double appearStart, double gulpStart, bool reduced)
    {
        if (reduced)
        {
            return new BubblePose(1, 1, 1, 0, 0, 0);
        }

        var appear = appearStart < 0 ? 1 : OutBack(Math.Clamp((now - appearStart) / AppearLength, 0, 1));
        var scaleX = 1.0;
        var scaleY = 1.0;
        var flash = 0.0;
        var glint = now % GlintPeriod / GlintLength;
        if (gulpStart >= 0)
        {
            var g = Math.Clamp((now - gulpStart) / GulpLength, 0, 1);
            var wobble = Math.Sin(g * 2 * Math.PI * GulpCycles) * Math.Pow(1 - g, 2) * GulpStretch;
            scaleX += wobble;
            scaleY -= wobble * SquashShare;
            flash = Math.Clamp(1 - (now - gulpStart) / FlashLength, 0, 1);
            if (g < 1)
            {
                glint = g * GulpLength / GlintLength;
            }
        }

        return new BubblePose(appear, scaleX, scaleY, flash, glint, now * IdleRate);
    }

    public static double OutBack(double x)
    {
        var c = Overshoot + 1;
        return 1 + c * Math.Pow(x - 1, 3) + Overshoot * Math.Pow(x - 1, 2);
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Skia paints are freed by their finalizers when the draw operation is collected; the render thread may still hold the art when the control detaches.")]
public sealed class BubbleFace : Control
{
    private static readonly TimeSpan FrameStep = TimeSpan.FromMilliseconds(33);

    private readonly BubbleArt _art = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _timer = new() { Interval = FrameStep };
    private readonly Motion _motion;
    private SKColor _tint = SKColors.Gray;
    private bool _lit;
    private double _appearStart = -1;
    private double _gulpStart = -1;

    public BubbleFace(Motion motion)
    {
        _motion = motion ?? throw new ArgumentNullException(nameof(motion));
        IsHitTestVisible = false;
        _timer.Tick += (_, _) => Advance();
    }

    public event Action<double>? Scaled;

    public BubblePose Pose => BubbleMotion.Sample(_clock.Elapsed.TotalSeconds, _appearStart, _gulpStart, _motion.Reduced);

    public void SetStatus(Color color, bool lit)
    {
        _tint = new SKColor(color.R, color.G, color.B);
        _lit = lit;
        InvalidateVisual();
    }

    public void Appear()
    {
        _appearStart = _clock.Elapsed.TotalSeconds;
        Advance();
    }

    public void Gulp()
    {
        _gulpStart = _clock.Elapsed.TotalSeconds;
        Advance();
    }

    public void SetRunning(bool running)
    {
        if (running && !_motion.Reduced)
        {
            _timer.Start();
        }
        else
        {
            _timer.Stop();
        }

        Advance();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.Custom(new FaceOperation(new Rect(Bounds.Size), _art, Pose, _tint, _lit, (float)(BubblePlacement.BodySize / 2)));
    }

    private void Advance()
    {
        var pose = Pose;
        Scaled?.Invoke(pose.Appear * (pose.ScaleX + pose.ScaleY) / 2);
        InvalidateVisual();
    }

    private sealed class FaceOperation(Rect bounds, BubbleArt art, BubblePose pose, SKColor tint, bool lit, float radius) : ICustomDrawOperation
    {
        public Rect Bounds => bounds;

        public bool Equals(ICustomDrawOperation? other) => false;

        public bool HitTest(Point p) => false;

        public void Dispose()
        {
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feature)
            {
                return;
            }

            using var lease = feature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            canvas.Translate((float)bounds.Width / 2, (float)bounds.Height / 2);
            canvas.Scale((float)pose.ScaleX, (float)pose.ScaleY);
            Paint(canvas, art, pose, tint, lit, radius);
            canvas.Restore();
        }
    }

    public static void Paint(SKCanvas canvas, BubbleArt art, BubblePose pose, SKColor tint, bool lit, float radius)
    {
        var drawn = radius * (float)pose.Appear;
        if (drawn < 1)
        {
            return;
        }

        art.DrawStatus(canvas, drawn, tint, lit);
        art.Draw(canvas, drawn, (float)pose.Clock, 0, 0, 1);
        art.DrawGlint(canvas, drawn, (float)pose.Glint);
        art.DrawFlash(canvas, drawn, (float)pose.Flash);
    }
}
