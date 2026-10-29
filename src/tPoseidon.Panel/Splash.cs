using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SkiaSharp;

namespace tPoseidon.Panel;

public sealed record SplashSeek(double Time);

public sealed record SplashBoxed(bool Boxed);

public sealed record SplashTarget(float X, float Y, float Size);

public sealed record SplashAim(SplashTarget Target);

public sealed record SplashRevealed(bool Revealed);

public static class SplashLayout
{
    public const double MainWidth = 680;
    public const double MainHeight = 520;
    public const double OverlayWidth = 1120;
    public const double OverlayHeight = 920;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Skia paints are freed by their finalizers when the composition handler is collected; the handler API has no disposal hook.")]
public sealed class SplashHandler : SeaHandler
{
    public const double Duration = 2.95;
    public const double HandoffAt = 2.3;
    public const float FlightStart = 1.68f;
    public const float FlightEnd = 2.42f;
    public const float PopAt = 2.42f;
    private const float BubbleStart = 1.38f;
    private const float BubbleRatio = 0.66f;
    private const float ArcHeight = 120;
    private const float IconSize = 210;
    private const float ViewBox = 276;
    private const float ViewCenter = 128;

    private sealed record Piece(SKPath Path, SKPaint Paint, bool AlsoFill)
    {
        public SKPathMeasure Measure { get; } = new(Path);
    }

    private sealed record Pop(Piece[] Pieces, float Cx, float Cy, float Start, float End, bool Squash);

    private sealed class Part
    {
        public required Piece[] Navy { get; init; }
        public required Piece[] Fill { get; init; }
        public required Piece Trace { get; init; }
        public required float Top { get; init; }
        public required float Bottom { get; init; }
        public required float[] Times { get; init; }
        public bool Tip { get; init; }
        public float PivotX { get; init; }
        public float PivotY { get; init; }
        public bool Squeeze { get; init; }
    }

    private static readonly SKColor Navy = SKColor.Parse("#0B2A5B");
    private static readonly SKColor TraceColor = SKColor.Parse("#BDF6FF");
    private static readonly SKColor FlashColor = SKColor.Parse("#E6FCFF");
    private readonly Part[] _navyOrder;
    private readonly Part[] _fillOrder;
    private readonly Piece _highlight;
    private readonly Piece _waveHighlight;
    private readonly SKPaint _waveNavy;
    private readonly SKPaint _waveFill;
    private readonly SKPaint _waveTrace;
    private readonly SKPath _wavePath = new();
    private readonly SKPath _scratch = new();
    private readonly SKPath _liquidPath = new();
    private readonly Pop[] _pops;
    private readonly SKPaint _layer = new();
    private readonly BubbleArt _art = new();
    private readonly SKPaint _liquid = new() { BlendMode = SKBlendMode.DstIn, IsAntialias = true, Style = SKPaintStyle.Fill, Color = SKColors.White };
    private readonly SKPaint _overlay = new() { BlendMode = SKBlendMode.SrcATop, IsAntialias = true };
    private readonly SKPaint _background = new() { Style = SKPaintStyle.Fill };
    private double? _seek;
    private bool _boxed;
    private bool _revealed;
    private bool _popNotified;
    private SplashTarget? _target;
    private readonly Action<bool>? _onPop;

    public SplashHandler(Action<bool>? onPop = null)
    {
        _onPop = onPop;
        var gradP = Gradient(4, 240, ("#A8F0FA", 0), ("#5CD6EE", 0.3f), ("#2FA8E6", 0.7f), ("#0D47A1", 1));
        var gradT = Gradient(14, 230, ("#E2FCFF", 0), ("#A8F0FA", 0.4f), ("#5CD6EE", 1));
        var gradW = SKShader.CreateLinearGradient(new SKPoint(18, 0), new SKPoint(238, 0), [SKColor.Parse("#5CD6EE"), SKColor.Parse("#2FA8E6")], [0, 1], SKShaderTileMode.Clamp);

        const string left = "M89 92 H42 Q30 92 30 80 V58";
        const string right = "M160 92 V58";
        const string shaft = "M89 212 V64";
        const string bowl = "M89 92 H146 C176 92 192 108 192 128 C192 148 176 164 146 164 H89";
        const string tipLeft = "M30 24 L46 62 Q30 54 14 62 Z";
        const string tipRight = "M160 24 L176 62 Q160 54 144 62 Z";
        const string tipCenter = "M89 6 L118 66 Q89 55 60 66 Z";

        Part LinePart(string d, float width, float fillWidth, SKShader shader, float top, float bottom, float[] times, bool squeeze = false) => new()
        {
            Navy = [Line(d, Navy, null, width)],
            Fill = [Line(d, default, shader, fillWidth)],
            Trace = Line(d, TraceColor, null, 4),
            Top = top,
            Bottom = bottom,
            Times = times,
            Squeeze = squeeze
        };

        Part TipPart(string d, SKShader shader, float px, float py, float top, float bottom, float[] times) => new()
        {
            Navy = [Solid(d, Navy, null, 12)],
            Fill = [Solid(d, default, shader, 2)],
            Trace = Line(d, TraceColor, null, 3.5f),
            Top = top,
            Bottom = bottom,
            Times = times,
            Tip = true,
            PivotX = px,
            PivotY = py
        };

        var armL = LinePart(left, 32, 20, gradT, 36, 112, [0.40f, 0.52f, 0.44f, 0.56f, 0.50f, 0.70f]);
        var armR = LinePart(right, 30, 18, gradT, 36, 112, [0.50f, 0.60f, 0.54f, 0.64f, 0.60f, 0.78f]);
        var shaftP = LinePart(shaft, 50, 38, gradP, 38, 238, [0.20f, 0.48f, 0.26f, 0.56f, 0.40f, 0.72f], true);
        var bowlP = LinePart(bowl, 42, 30, gradP, 70, 186, [0.62f, 0.92f, 0.68f, 0.98f, 0.84f, 1.12f]);
        var tipL = TipPart(tipLeft, gradT, 30, 62, 10, 76, [0.46f, 0.58f, 0.56f, 0.70f, 0.64f, 0.86f]);
        var tipC = TipPart(tipCenter, gradP, 89, 66, 0, 80, [0.52f, 0.64f, 0.62f, 0.76f, 0.70f, 0.92f]);
        var tipR = TipPart(tipRight, gradT, 160, 62, 10, 76, [0.58f, 0.70f, 0.68f, 0.82f, 0.76f, 0.98f]);
        _navyOrder = [armL, armR, shaftP, bowlP, tipL, tipR, tipC];
        _fillOrder = [armL, armR, tipL, tipR, shaftP, bowlP, tipC];

        _highlight = Line("M80 104 V196", SKColors.White.WithAlpha(140), null, 6);
        _waveHighlight = Line("M24 220 Q40 210 56 212", SKColors.White.WithAlpha(204), null, 4);
        _waveNavy = Stroke(Navy, null, 28);
        _waveFill = Stroke(default, gradW, 16);
        _waveTrace = Stroke(TraceColor, null, 4);

        _pops =
        [
            new Pop([Disc(146, 128, 12, SKColor.Parse("#F2FDFF")), Disc(142, 124, 3.5f, SKColors.White)], 146, 128, 0.94f, 1.16f, true),
            new Pop([Disc(222, 52, 11, SKColors.White.WithAlpha(46)), Ring(222, 52, 11, 4), Disc(218.15f, 48.15f, 2.75f, SKColors.White)], 222, 52, 1.0f, 1.22f, false),
            new Pop([Disc(238, 24, 6, SKColors.White.WithAlpha(46)), Ring(238, 24, 6, 3), Disc(235.9f, 21.9f, 1.5f, SKColors.White)], 238, 24, 1.07f, 1.3f, false)
        ];
    }

    private static SKShader Gradient(float y0, float y1, params (string Color, float Offset)[] stops)
        => SKShader.CreateLinearGradient(new SKPoint(0, y0), new SKPoint(0, y1), stops.Select(s => SKColor.Parse(s.Color)).ToArray(), stops.Select(s => s.Offset).ToArray(), SKShaderTileMode.Clamp);

    private static SKPaint Stroke(SKColor color, SKShader? shader, float width) => new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
        Color = shader is null ? color : SKColors.White,
        Shader = shader
    };

    private static SKPaint FillPaint(SKColor color, SKShader? shader) => new() { IsAntialias = true, Style = SKPaintStyle.Fill, Color = color, Shader = shader };

    private static Piece Line(string d, SKColor color, SKShader? shader, float width) => new(SKPath.ParseSvgPathData(d), Stroke(color, shader, width), false);

    private static Piece Solid(string d, SKColor color, SKShader? shader, float width) => new(SKPath.ParseSvgPathData(d), Stroke(color, shader, width), true);

    private static Piece Disc(float cx, float cy, float r, SKColor color)
    {
        var path = new SKPath();
        path.AddCircle(cx, cy, r);
        return new Piece(path, FillPaint(color, null), false);
    }

    private static Piece Ring(float cx, float cy, float r, float width)
    {
        var path = new SKPath();
        path.AddCircle(cx, cy, r);
        return new Piece(path, Stroke(SKColor.Parse("#2FA8E6"), null, width), false);
    }

    protected override bool HasTransient(TimeSpan now) => _seek is null && T < Duration + 0.4;

    protected override void OnCustomMessage(object message)
    {
        if (message is SplashSeek seek)
            _seek = seek.Time;
        else if (message is SplashBoxed boxed)
            _boxed = boxed.Boxed;
        else if (message is SplashAim aim)
            _target = aim.Target;
        else if (message is SplashRevealed revealed)
            _revealed = revealed.Revealed;
    }

    private static float Clamp01(float x) => Math.Clamp(x, 0, 1);

    private static float Span(float t, float from, float to) => Clamp01((t - from) / (to - from));

    private static float OutCubic(float x) => 1 - MathF.Pow(1 - Clamp01(x), 3);

    private static float InOut(float x)
    {
        x = Clamp01(x);
        return x < 0.5f ? 4 * x * x * x : 1 - MathF.Pow(-2 * x + 2, 3) / 2;
    }

    private static float OutBack(float x)
    {
        x = Clamp01(x);
        const float c1 = 2.4f;
        const float c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(x - 1, 3) + c1 * MathF.Pow(x - 1, 2);
    }

    private static float ElasticOut(float x)
    {
        x = Clamp01(x);
        if (x <= 0 || x >= 1)
            return x;
        return MathF.Pow(2, -10 * x) * MathF.Sin((10 * x - 0.75f) * (2 * MathF.PI / 3)) + 1;
    }

    private static float BackSoft(float x)
    {
        x = Clamp01(x);
        const float c1 = 1.3f;
        const float c3 = c1 + 1;
        return 1 + c3 * MathF.Pow(x - 1, 3) + c1 * MathF.Pow(x - 1, 2);
    }

    private static void DrawPieces(SKCanvas canvas, Piece[] pieces)
    {
        foreach (var piece in pieces)
        {
            canvas.DrawPath(piece.Path, piece.Paint);
            if (piece.AlsoFill)
            {
                using var fill = piece.Paint.Clone();
                fill.Style = SKPaintStyle.Fill;
                canvas.DrawPath(piece.Path, fill);
            }
        }
    }

    private void DrawTrimmed(SKCanvas canvas, Piece piece, float progress)
    {
        if (progress <= 0)
            return;
        if (progress >= 1)
        {
            canvas.DrawPath(piece.Path, piece.Paint);
            return;
        }
        _scratch.Rewind();
        piece.Measure.GetSegment(0, piece.Measure.Length * progress, _scratch, true);
        canvas.DrawPath(_scratch, piece.Paint);
    }

    private static void ApplyPartTransform(SKCanvas canvas, Part part, float t, float body)
    {
        if (part.Tip)
        {
            canvas.Translate(part.PivotX, part.PivotY);
            canvas.Scale(body, body);
            canvas.Translate(-part.PivotX, -part.PivotY);
        }
        else if (part.Squeeze)
        {
            var sx = 0.84f + 0.16f * MathF.Min(1, ElasticOut(Span(t, part.Times[2], part.Times[3] + 0.3f)));
            canvas.Translate(89, 0);
            canvas.Scale(sx, 1);
            canvas.Translate(-89, 0);
        }
    }

    private static float BodyProgress(Part part, float t)
        => part.Tip ? BackSoft(Span(t, part.Times[2], part.Times[3])) : OutCubic(Span(t, part.Times[2], part.Times[3]));

    private void DrawTrace(SKCanvas canvas, Part part, float t)
    {
        var body = Span(t, part.Times[2], part.Times[3]);
        if (body >= 1)
            return;
        DrawTrimmed(canvas, part.Trace, OutCubic(Span(t, part.Times[0], part.Times[1])));
    }

    private void DrawNavy(SKCanvas canvas, Part part, float t)
    {
        var body = BodyProgress(part, t);
        if (body <= 0)
            return;
        canvas.Save();
        ApplyPartTransform(canvas, part, t, body);
        if (part.Tip)
            DrawPieces(canvas, part.Navy);
        else
            foreach (var piece in part.Navy)
                DrawTrimmed(canvas, piece, body);
        canvas.Restore();
    }

    private void DrawLiquidMask(SKCanvas canvas, float top, float bottom, float level, float t)
    {
        var edge = bottom + 6 - (bottom - top + 12) * level;
        var amplitude = 3.2f * MathF.Sin(MathF.PI * level);
        _liquidPath.Rewind();
        _liquidPath.MoveTo(-20, bottom + 60);
        for (var x = -20f; x <= ViewBox + 20; x += 6)
            _liquidPath.LineTo(x, edge + amplitude * MathF.Sin(x * 0.11f + t * 16));
        _liquidPath.LineTo(ViewBox + 20, bottom + 60);
        _liquidPath.Close();
        canvas.DrawPath(_liquidPath, _liquid);
    }

    private void DrawFlash(SKCanvas canvas, float flash)
    {
        _overlay.Shader = null;
        _overlay.Color = FlashColor.WithAlpha((byte)(120 * flash));
        canvas.DrawRect(-20, -20, ViewBox + 40, ViewBox + 40, _overlay);
    }

    private void DrawFill(SKCanvas canvas, Part part, float t)
    {
        var body = BodyProgress(part, t);
        var level = OutCubic(Span(t, part.Times[4], part.Times[5]));
        if (body <= 0 || level <= 0)
            return;
        var flash = 1 - OutCubic(Span(t, part.Times[5], part.Times[5] + 0.24f));
        var layered = level < 1 || flash > 0;
        canvas.Save();
        ApplyPartTransform(canvas, part, t, body);
        if (layered)
            canvas.SaveLayer(null);
        if (part.Tip)
            DrawPieces(canvas, part.Fill);
        else
            foreach (var piece in part.Fill)
                DrawTrimmed(canvas, piece, body);
        if (level < 1)
            DrawLiquidMask(canvas, part.Top, part.Bottom, level, t);
        if (flash > 0)
            DrawFlash(canvas, flash);
        if (layered)
            canvas.Restore();
        canvas.Restore();
    }

    private void DrawWave(SKCanvas canvas, float t)
    {
        var rise = Span(t, 0.02f, 0.4f);
        if (rise <= 0)
            return;
        var ripple = 20 * (0.25f + 0.75f * ElasticOut(Span(t, 0.06f, 0.95f)));
        _wavePath.Rewind();
        _wavePath.MoveTo(18, 226);
        _wavePath.QuadTo(50, 226 - ripple, 82, 226);
        _wavePath.QuadTo(114, 226 + ripple, 146, 226);
        _wavePath.QuadTo(178, 226 - ripple, 210, 226);
        using var measure = new SKPathMeasure(_wavePath);
        var trace = OutCubic(Span(t, 0.04f, 0.34f));
        var body = OutCubic(Span(t, 0.1f, 0.44f));
        var level = OutCubic(Span(t, 0.26f, 0.62f));
        var flash = 1 - OutCubic(Span(t, 0.62f, 0.86f));
        using var bodyPath = new SKPath();
        measure.GetSegment(0, measure.Length * body, bodyPath, true);
        canvas.Save();
        canvas.ClipRect(new SKRect(-10, -10, ViewBox - 10, 240.5f));
        canvas.Translate(0, 30 * (1 - BackSoft(rise)));
        if (body < 1)
        {
            _scratch.Rewind();
            measure.GetSegment(0, measure.Length * trace, _scratch, true);
            canvas.DrawPath(_scratch, _waveTrace);
        }
        canvas.DrawPath(bodyPath, _waveNavy);
        if (level > 0)
        {
            var layered = level < 1 || flash > 0;
            if (layered)
                canvas.SaveLayer(null);
            canvas.DrawPath(bodyPath, _waveFill);
            if (level < 1)
                DrawLiquidMask(canvas, 205, 242, level, t);
            if (flash > 0)
                DrawFlash(canvas, flash);
            if (layered)
                canvas.Restore();
        }
        var shine = Span(t, 0.5f, 0.8f);
        if (shine > 0)
        {
            _waveHighlight.Paint.Color = SKColors.White.WithAlpha((byte)(204 * shine));
            DrawTrimmed(canvas, _waveHighlight, OutCubic(shine));
        }
        canvas.Restore();
    }

    private void DrawEmblem(SKCanvas canvas, float t)
    {
        foreach (var part in _navyOrder)
            DrawTrace(canvas, part, t);
        foreach (var part in _navyOrder)
            DrawNavy(canvas, part, t);
        foreach (var part in _fillOrder)
            DrawFill(canvas, part, t);
        var shine = Span(t, 0.72f, 0.98f);
        if (shine > 0)
            DrawTrimmed(canvas, _highlight, OutCubic(shine));
        DrawWave(canvas, t);
        foreach (var pop in _pops)
        {
            var p = Span(t, pop.Start, pop.End);
            if (p <= 0)
                continue;
            var sx = pop.Squash ? ElasticOut(p) : OutBack(p);
            var sy = pop.Squash ? ElasticOut(Span(p, 0.14f, 1)) : sx;
            canvas.Save();
            canvas.Translate(pop.Cx, pop.Cy);
            canvas.Scale(sx, sy);
            canvas.Translate(-pop.Cx, -pop.Cy);
            DrawPieces(canvas, pop.Pieces);
            canvas.Restore();
        }

        var shimmer = MathF.Sin(MathF.PI * Span(t, 1.1f, 1.5f));
        if (shimmer > 0)
        {
            _overlay.Shader = null;
            _overlay.Color = SKColors.White.WithAlpha((byte)(110 * shimmer));
            canvas.DrawRect(-10, -10, ViewBox, ViewBox, _overlay);
        }

        var glint = Span(t, 1.08f, 1.66f);
        if (glint > 0 && glint < 1)
        {
            var gx = -60 + (ViewBox + 120) * OutCubic(glint);
            canvas.Save();
            canvas.RotateDegrees(22, gx, ViewCenter);
            using var shader = SKShader.CreateLinearGradient(new SKPoint(gx - 26, 0), new SKPoint(gx + 26, 0), [SKColors.White.WithAlpha(0), SKColors.White, SKColors.White.WithAlpha(0)], [0, 0.5f, 1], SKShaderTileMode.Clamp);
            _overlay.Shader = shader;
            _overlay.Color = SKColors.White;
            canvas.DrawRect(gx - 26, -120, 52, ViewBox + 240, _overlay);
            _overlay.Shader = null;
            canvas.Restore();
        }
    }

    public static SplashTarget DefaultTarget(float width, float height) => new(
        (width - (float)SplashLayout.MainWidth) / 2 + 40,
        (height - (float)SplashLayout.MainHeight) / 2 + 39,
        30);

    public static (float X, float Y, float Size, float Flight) Anchor(float t, float width, float height, SplashTarget? target)
    {
        target ??= DefaultTarget(width, height);
        var flight = InOut(Span(t, FlightStart, FlightEnd));
        var centerX = width / 2;
        var centerY = height / 2 - 6;
        var targetX = target.X;
        var targetY = target.Y;
        var dx = targetX - centerX;
        var dy = targetY - centerY;
        var length = MathF.Sqrt(dx * dx + dy * dy);
        var nx = -dy / length;
        var ny = dx / length;
        var envelope = MathF.Sin(MathF.PI * flight);
        var offset = envelope * ArcHeight + MathF.Sin(flight * MathF.PI * 5) * envelope * 9;
        return (centerX + dx * flight + nx * offset, centerY + dy * flight + ny * offset, IconSize + (target.Size - IconSize) * flight, flight);
    }

    protected override void Draw(SKCanvas canvas, float width, float height)
    {
        var t = (float)(_seek ?? T);
        var anchor = Anchor(t, width, height, _target);
        var ahead = Anchor(t + 0.012f, width, height, _target);
        var travelAngle = MathF.Atan2(ahead.Y - anchor.Y, ahead.X - anchor.X) * 180 / MathF.PI;
        var speed = MathF.Sin(MathF.PI * anchor.Flight);
        if (_boxed)
        {
            _background.Color = PanelTokens.Surface0.WithAlpha((byte)(255 * (1 - InOut(Span(t, (float)HandoffAt, (float)HandoffAt + 0.4f)))));
            canvas.DrawRect(0, 0, width, height, _background);
        }

        var popP = Span(t, PopAt, PopAt + 0.14f);
        var bubbleIn = OutBack(Span(t, BubbleStart, BubbleStart + 0.3f));
        var bubbleAlpha = Clamp01(bubbleIn * 1.4f) * (1 - popP);
        var carried = Span(t, BubbleStart, BubbleStart + 0.3f) * (1 - popP);
        var bob = carried * MathF.Sin(t * 8.5f) * (1 - InOut(Span(t, PopAt - 0.2f, PopAt)));
        var landed = t >= PopAt;
        if (landed != _popNotified)
        {
            _popNotified = landed;
            _onPop?.Invoke(landed);
        }
        var form = 0.9f + 0.1f * ElasticOut(Span(t, 0f, 0.9f));
        var k = anchor.Size / IconSize * form * (IconSize / ViewBox);
        var alpha = Span(t, 0f, 0.06f);
        if (alpha <= 0)
            return;

        if (!_revealed)
        {
            canvas.Save();
            canvas.Translate(anchor.X, anchor.Y + bob * anchor.Size * 0.022f);
            canvas.RotateDegrees(bob * 2.5f);
            canvas.Scale(k, k);
            canvas.Translate(-ViewCenter, -ViewCenter);
            _layer.Color = SKColors.White.WithAlpha((byte)(255 * alpha));
            canvas.SaveLayer(_layer);
            DrawEmblem(canvas, t);
            canvas.Restore();
            canvas.Restore();
        }

        var radius = anchor.Size * BubbleRatio * bubbleIn * (1 + 0.14f * popP);
        if (bubbleAlpha > 0.01f && radius > 1)
        {
            canvas.Save();
            canvas.Translate(anchor.X, anchor.Y);
            _art.Draw(canvas, radius, t, travelAngle, speed, bubbleAlpha);
            canvas.Restore();
        }
        if (popP > 0)
            _art.DrawPop(canvas, anchor.X, anchor.Y, anchor.Size * BubbleRatio, Span(t, PopAt, PopAt + 0.5f));
    }
}

public sealed class SplashWindow : Window
{
    private readonly Action<SplashWindow> _onHandoff;
    private readonly SeaHost _sea;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
    private readonly Delegate? _subclass;
    private bool _handed;
    private bool _closing;
    private bool _boxed;
    private SplashTarget? _target;
    private MainWindow? _main;
    private bool _holding;

    public SplashWindow(Action<SplashWindow> onHandoff, bool autoplay = true)
    {
        _onHandoff = onHandoff;
        if (OperatingSystem.IsWindows())
        {
            _subclass = new NativeWindows.SubclassProc(OnSubclass);
        }

        Width = SplashLayout.OverlayWidth;
        Height = SplashLayout.OverlayHeight;
        if (Screens.Primary is { } primary)
        {
            Width = Math.Min(Width, primary.WorkingArea.Width / primary.Scaling);
            Height = Math.Min(Height, primary.WorkingArea.Height / primary.Scaling);
        }
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        SystemDecorations = SystemDecorations.None;
        WindowStartupLocation = WindowStartupLocation.Manual;
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Background = Brushes.Transparent;
        Title = "tPoseidon";
        Icon = PanelAssets.WindowIcon();
        Transitions = [new Avalonia.Animation.DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(200) }];

        _sea = new SeaHost(new SplashHandler(OnPop), Motion.Shared);
        Content = _sea;
        PointerPressed += (_, _) => Skip();
        KeyDown += (_, e) => { if (e.Key is Key.Escape or Key.Enter or Key.Space) Skip(); };
        Activated += (_, _) => Motion.Shared.SetFocused(true);
        Opened += (_, _) =>
        {
            if (OperatingSystem.IsWindows() && _subclass is NativeWindows.SubclassProc subclass && TryGetPlatformHandle() is { } handle && !NativeWindows.Subclass(handle.Handle, subclass))
                UseBoxedFallback();
            if (ActualTransparencyLevel != WindowTransparencyLevel.Transparent)
                UseBoxedFallback();
            Activate();
            if (autoplay)
            {
                _clock.Restart();
                _timer.Tick += (_, _) => OnTick();
                _timer.Start();
            }
        };
    }

    public void Bind(MainWindow main, bool hold = true)
    {
        _main = main;
        _holding = hold;
        if (hold)
            main.Hold();
        else
            main.SetEmblemVisible(false);
        main.Opened += (_, _) => Aim();
        Closed += (_, _) => main.SetEmblemVisible(true);
    }

    private void OnPop(bool popped) => Dispatcher.UIThread.Post(() =>
    {
        _main?.SetEmblemVisible(popped);
        _sea.Send(new SplashRevealed(popped));
    }, DispatcherPriority.Send);

    public void Aim()
    {
        if (_main?.EmblemScreenBox() is not { } box)
            return;
        var local = this.PointToClient(new PixelPoint((int)Math.Round(box.X), (int)Math.Round(box.Y)));
        var scale = RenderScaling;
        var target = new SplashTarget((float)local.X, (float)local.Y, (float)(box.Size / scale));
        if (target == _target)
            return;
        _target = target;
        _sea.Send(new SplashAim(target));
    }

    public void PlaceOverScreenCenter()
    {
        var screen = Screens.Primary ?? Screens.All[0];
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var origin = new PixelPoint(
            area.X + (int)Math.Round((area.Width - SplashLayout.MainWidth * scale) / 2),
            area.Y + (int)Math.Round((area.Height - SplashLayout.MainHeight * scale) / 2));
        var dx = (int)Math.Round((Width - SplashLayout.MainWidth) / 2 * scale);
        var dy = (int)Math.Round((Height - SplashLayout.MainHeight) / 2 * scale);
        Position = new PixelPoint(origin.X - dx, origin.Y - dy);
    }

    public void UseBoxedFallback()
    {
        _boxed = true;
        Width = SplashLayout.MainWidth;
        Height = SplashLayout.MainHeight;
        Background = Tokens.Surface0;
        _sea.Send(new SplashBoxed(true));
        PlaceOverScreenCenter();
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private IntPtr OnSubclass(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, UIntPtr id, UIntPtr data)
    {
        if (message == NativeWindows.HitTestMessage && !_boxed)
        {
            var packed = lParam.ToInt64();
            var sx = (short)(packed & 0xFFFF);
            var sy = (short)((packed >> 16) & 0xFFFF);
            var scale = RenderScaling;
            var anchor = SplashHandler.Anchor((float)_clock.Elapsed.TotalSeconds, (float)Bounds.Width, (float)Bounds.Height, _target);
            var cx = Position.X + anchor.X * scale;
            var cy = Position.Y + anchor.Y * scale;
            var inside = !_handed && Math.Sqrt((sx - cx) * (sx - cx) + (sy - cy) * (sy - cy)) <= anchor.Size * 0.66 * scale;
            return inside ? NativeWindows.HitClient : NativeWindows.HitTransparent;
        }
        return NativeWindows.DefaultProc(window, message, wParam, lParam);
    }

    private void OnTick()
    {
        var elapsed = _clock.Elapsed.TotalSeconds;
        Aim();
        if (!_handed && elapsed >= SplashHandler.HandoffAt)
            Handoff();
        if (elapsed >= SplashHandler.Duration + 0.05)
            CloseNow();
    }

    private void Handoff()
    {
        if (_handed)
            return;
        _handed = true;
        _onHandoff(this);
        if (_holding)
            _main?.Release();
    }

    private void Skip()
    {
        if (_closing)
            return;
        Handoff();
        _closing = true;
        _timer.Stop();
        Opacity = 0;
        DispatcherTimer.RunOnce(Close, TimeSpan.FromMilliseconds(220));
    }

    private void CloseNow()
    {
        if (_closing)
            return;
        _closing = true;
        _timer.Stop();
        Close();
    }
}
