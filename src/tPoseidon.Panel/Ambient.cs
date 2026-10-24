using Avalonia;
using SkiaSharp;

namespace tPoseidon.Panel;

public static class PanelTokens
{
    public static readonly SKColor Surface0 = new(0x14, 0x17, 0x1C);
    public static readonly SKColor Surface1 = new(0x1B, 0x1F, 0x26);
    public static readonly SKColor HeaderDeep = new(0x17, 0x27, 0x40);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Skia paints are freed by their finalizers when the composition handler is collected; the handler API has no disposal hook.")]
public sealed class HeaderSeaHandler : SeaHandler
{
    private static int s_frames;

    private sealed record WaveLayer(float Baseline, float Amplitude, float Wavelength, float Speed, SKPaint Paint, SKPaint? Crest);

    private sealed record Blob(float X, float Radius, float Ax, float F, float Phase);

    private readonly WaveLayer[] _layers =
    [
        new(-10, 2.2f, 150, 7, Fill(SeaPalette.Sky.WithAlpha(46)), null),
        new(-7, 2.0f, 110, 11, Fill(SeaPalette.Aqua.WithAlpha(40)), null),
        new(-4, 1.6f, 84, 15, Fill(PanelTokens.Surface1), Stroke(SeaPalette.Foam.WithAlpha(70), 1)),
    ];

    private readonly Blob[] _blobs =
    [
        new(0.12f, 46, 30, 0.10f, 0.0f),
        new(0.36f, 58, 36, 0.07f, 1.7f),
        new(0.62f, 50, 32, 0.09f, 3.1f),
        new(0.86f, 60, 38, 0.06f, 4.4f),
    ];

    private readonly SKPath?[] _paths = new SKPath?[3];
    private readonly SKPaint _caustic = new()
    {
        IsAntialias = true,
        Shader = SKShader.CreateRadialGradient(SKPoint.Empty, 1, [SeaPalette.Light.WithAlpha(30), SeaPalette.Light.WithAlpha(0)], [0, 1], SKShaderTileMode.Clamp),
    };

    private SKPaint? _background;
    private float _builtWidth;
    private float _builtHeight;

    public static int Frames => Volatile.Read(ref s_frames);

    private void Build(float width, float height)
    {
        if (width == _builtWidth && height == _builtHeight)
            return;
        _builtWidth = width;
        _builtHeight = height;
        _background = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(0, height),
                [PanelTokens.Surface1, PanelTokens.HeaderDeep], [0.25f, 1], SKShaderTileMode.Clamp),
        };
        for (var i = 0; i < _layers.Length; i++)
        {
            var layer = _layers[i];
            _paths[i]?.Dispose();
            _paths[i] = WavePath(width + layer.Wavelength, height + layer.Baseline, layer.Amplitude, layer.Wavelength, height + 2);
        }
    }

    protected override void Draw(SKCanvas canvas, float width, float height)
    {
        Build(width, height);
        var t = (float)T;
        canvas.DrawRect(0, 0, width, height, _background);
        foreach (var b in _blobs)
        {
            var x = width * b.X + MathF.Sin(t * b.F * 2 + b.Phase) * b.Ax;
            var r = b.Radius * (0.85f + 0.15f * MathF.Sin(t * 0.45f + b.Phase));
            canvas.Save();
            canvas.Translate(x, height * 0.55f);
            canvas.Scale(r, r * 0.32f);
            canvas.DrawCircle(0, 0, 1, _caustic);
            canvas.Restore();
        }
        for (var i = 0; i < _layers.Length; i++)
        {
            var layer = _layers[i];
            canvas.Save();
            canvas.Translate(-(float)(T * layer.Speed % layer.Wavelength), 0);
            canvas.DrawPath(_paths[i], layer.Paint);
            if (layer.Crest is not null)
                canvas.DrawPath(_paths[i], layer.Crest);
            canvas.Restore();
        }
        Interlocked.Increment(ref s_frames);
    }
}

public sealed class ListSeaHandler : SeaHandler
{
    private const float FishPeriod = 70;
    private const float FishScale = 0.5f;

    private sealed record Bubble(float X, float Speed, float Radius, float Sway, float Phase);

    private readonly Bubble[] _bubbles;
    private readonly Rect[] _previous;
    private Rect _previousFish;
    private readonly SKPaint _fill = Fill(SeaPalette.Foam.WithAlpha(14));
    private readonly SKPaint _ring = Stroke(SeaPalette.Light.WithAlpha(70), 1);
    private readonly SKPaint _shine = Fill(SeaPalette.Foam.WithAlpha(120));
    private readonly SKPaint _fishBody = Fill(SeaPalette.Fish.WithAlpha(210));
    private readonly SKPaint _fishBelly = Fill(SeaPalette.Peach.WithAlpha(210));
    private readonly SKPaint _fishLine = Stroke(SeaPalette.Navy, 2);
    private readonly SKPaint _eye = Fill(SeaPalette.Navy);
    private readonly SKPaint _eyeShine = Fill(SKColors.White);

    public ListSeaHandler()
    {
        var random = new Random(11);
        _bubbles = Enumerable.Range(0, 9)
            .Select(i => new Bubble(
                (i + 0.5f) / 9f + (float)(random.NextDouble() - 0.5) * 0.06f,
                8 + (float)random.NextDouble() * 10,
                1.5f + (float)random.NextDouble() * 2.5f,
                3 + (float)random.NextDouble() * 5,
                (float)random.NextDouble()))
            .ToArray();
        _previous = new Rect[_bubbles.Length];
    }

    private SKPoint BubbleAt(Bubble b, float width, float height)
    {
        var travel = height + b.Radius * 2 + 20;
        var progress = ((float)T * b.Speed + b.Phase * travel) % travel;
        return new SKPoint(width * b.X + MathF.Sin((float)T * 0.8f + b.Phase * 6.28f) * b.Sway, height + b.Radius + 10 - progress);
    }

    private (SKPoint Position, bool FacingRight) FishAt(float width, float height)
    {
        var margin = 40f;
        var lane = MathF.Max(1, width - margin * 2);
        var cycle = (float)T / FishPeriod % 1f;
        var leg = cycle < 0.5f ? cycle * 2 : (1 - cycle) * 2;
        var eased = leg * leg * (3 - 2 * leg);
        return (new SKPoint(margin + lane * eased, height - 14 + MathF.Sin((float)T * 1.3f) * 2), cycle < 0.5f);
    }

    protected override void InvalidateFrame()
    {
        var size = EffectiveSize;
        float w = (float)size.X, h = (float)size.Y;
        for (var i = 0; i < _bubbles.Length; i++)
        {
            var b = _bubbles[i];
            var p = BubbleAt(b, w, h);
            var pad = b.Radius + 2;
            var rect = new Rect(p.X - pad, p.Y - pad, pad * 2, pad * 2);
            Invalidate(_previous[i].Union(rect));
            _previous[i] = rect;
        }
        var fish = FishAt(w, h).Position;
        var fishRect = new Rect(fish.X - 16, fish.Y - 12, 32, 22);
        Invalidate(_previousFish.Union(fishRect));
        _previousFish = fishRect;
    }

    protected override void Draw(SKCanvas canvas, float width, float height)
    {
        foreach (var b in _bubbles)
        {
            var p = BubbleAt(b, width, height);
            canvas.DrawCircle(p, b.Radius, _fill);
            canvas.DrawCircle(p, b.Radius, _ring);
            canvas.DrawCircle(p.X - b.Radius * 0.35f, p.Y - b.Radius * 0.35f, MathF.Max(0.6f, b.Radius * 0.28f), _shine);
        }
        DrawFish(canvas, width, height);
    }

    private void DrawFish(SKCanvas canvas, float width, float height)
    {
        var (p, right) = FishAt(width, height);
        var t = (float)T;
        canvas.Save();
        canvas.Translate(p.X, p.Y);
        canvas.Scale(right ? FishScale : -FishScale, FishScale);
        var wiggle = MathF.Sin(t * 7) * 0.22f;
        canvas.Save();
        canvas.Translate(-11, 0);
        canvas.RotateRadians(wiggle);
        using (var tail = new SKPath())
        {
            tail.MoveTo(0, 0);
            tail.LineTo(-10, -7);
            tail.QuadTo(-7, 0, -10, 7);
            tail.Close();
            canvas.DrawPath(tail, _fishBody);
            canvas.DrawPath(tail, _fishLine);
        }
        canvas.Restore();
        var body = new SKRect(-13, -9, 13, 9);
        canvas.DrawOval(body, _fishBody);
        canvas.DrawOval(new SKRect(-8, 1, 10, 8), _fishBelly);
        canvas.DrawOval(body, _fishLine);
        canvas.DrawCircle(6, -2, 3, _eye);
        canvas.DrawCircle(6.8f, -2.8f, 1.2f, _eyeShine);
        canvas.Restore();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001", Justification = "Skia paints are freed by their finalizers when the composition handler is collected; the handler API has no disposal hook.")]
public sealed class ShellBadgeHandler : SeaHandler
{
    private const float Unit = 0.25f;
    private static readonly SKPoint Anchor = new(44, 50);
    private static readonly SKPoint Pearl = new(44, 62);
    private static readonly TimeSpan RippleLength = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan ShakeLength = TimeSpan.FromMilliseconds(600);

    private readonly SKPaint _outline = Stroke(SeaPalette.Navy, 5);
    private readonly SKPaint _rib = Stroke(new SKColor(0xD9, 0x86, 0x66, 0xB0), 3);
    private readonly SKPaint _shellLit;
    private readonly SKPaint _shellDim = Fill(new SKColor(0x4A, 0x55, 0x68));
    private readonly SKPaint _pearl = Fill(SKColors.White);
    private readonly SKPaint _shine = Fill(SKColors.White.WithAlpha(230));
    private readonly SKPaint _ring = Stroke(SeaPalette.Light, 5);
    private readonly SKPaint _glow = new() { IsAntialias = true };
    private readonly SKShader _aquaGlow = Glow(SeaPalette.Aqua);
    private readonly SKShader _coralGlow = Glow(SeaPalette.Coral);
    private readonly SKPath _shell = new();
    private readonly SKPath _ribs = new();
    private ServerStatus _status = ServerStatus.Stopped;
    private TimeSpan? _rippleAt;
    private TimeSpan? _shakeAt;

    public ShellBadgeHandler()
    {
        _shellLit = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(new SKPoint(0, 10), new SKPoint(0, 70), [SeaPalette.Peach, SeaPalette.Shell], [0, 1], SKShaderTileMode.Clamp),
        };
        var hinge = new SKPoint(44, 70);
        var center = new SKPoint(44, 52);
        const int bumps = 5;
        const float start = 200, end = 340, radius = 34;
        SKPoint At(float deg, float r) => new(center.X + r * MathF.Cos(deg * MathF.PI / 180), center.Y + r * MathF.Sin(deg * MathF.PI / 180));
        _shell.MoveTo(hinge);
        _shell.LineTo(At(start, radius));
        for (var i = 0; i < bumps; i++)
        {
            var a0 = start + (end - start) * i / bumps;
            var a1 = start + (end - start) * (i + 1) / bumps;
            _shell.QuadTo(At((a0 + a1) / 2, radius + 10), At(a1, radius));
            if (i < bumps - 1)
            {
                _ribs.MoveTo(hinge.X, hinge.Y - 6);
                _ribs.LineTo(At(a1, radius - 6));
            }
        }
        _shell.Close();
    }

    private static SKShader Glow(SKColor color) =>
        SKShader.CreateRadialGradient(Pearl, 50, [color.WithAlpha(190), color.WithAlpha(0)], [0, 1], SKShaderTileMode.Clamp);

    protected override bool WantsAmbientFrames => _status == ServerStatus.Running;

    protected override bool HasTransient(TimeSpan now) =>
        (_rippleAt is { } r && now - r < RippleLength) || (_shakeAt is { } s && now - s < ShakeLength);

    protected override void OnCustomMessage(object message)
    {
        if (message is not BadgeState state)
            return;
        _status = state.Status;
        if (state.Ripple && EventsOn)
            _rippleAt = Now;
        if (state.Shake && EventsOn)
            _shakeAt = Now;
    }

    protected override void Draw(SKCanvas canvas, float width, float height)
    {
        var now = Now;
        var running = _status == ServerStatus.Running;
        var crashed = _status == ServerStatus.Crashed;
        canvas.Save();
        canvas.Translate(width / 2, height / 2);
        if (_shakeAt is { } shake && now - shake < ShakeLength && EventsOn)
        {
            var p = (float)((now - shake).TotalMilliseconds / ShakeLength.TotalMilliseconds);
            canvas.Translate(MathF.Sin(p * 40) * 2.5f * (1 - p), 0);
        }
        canvas.Scale(Unit);
        canvas.Translate(-Anchor.X, -Anchor.Y);
        if (running || crashed)
        {
            var pulse = AmbientOn && running ? 0.7f + 0.3f * MathF.Sin((float)T * 2.2f) : 0.85f;
            _glow.Shader = crashed ? _coralGlow : _aquaGlow;
            _glow.Color = SKColors.White.WithAlpha((byte)(255 * pulse));
            canvas.DrawCircle(Pearl, 50, _glow);
        }
        canvas.DrawPath(_shell, running || crashed ? _shellLit : _shellDim);
        canvas.DrawPath(_ribs, _rib);
        canvas.DrawPath(_shell, _outline);
        _pearl.Color = crashed ? SeaPalette.Coral : running ? SeaPalette.Foam : new SKColor(0x8F, 0x9A, 0xAC);
        canvas.DrawCircle(Pearl, 10, _pearl);
        canvas.DrawCircle(Pearl, 10, _outline);
        if (running)
            canvas.DrawCircle(Pearl.X - 3, Pearl.Y - 3, 3, _shine);
        if (_rippleAt is { } ripple && now - ripple < RippleLength && EventsOn)
        {
            var p = (float)((now - ripple).TotalMilliseconds / RippleLength.TotalMilliseconds);
            for (var k = 0; k < 2; k++)
            {
                var q = p - k * 0.3f;
                if (q <= 0)
                    continue;
                _ring.Color = SeaPalette.Light.WithAlpha((byte)(230 * (1 - q)));
                canvas.DrawCircle(Pearl, 12 + 32 * q, _ring);
            }
        }
        canvas.Restore();
    }
}
