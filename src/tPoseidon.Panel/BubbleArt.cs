using SkiaSharp;

namespace tPoseidon.Panel;

public sealed class BubbleArt : IDisposable
{
    private const float GlintTilt = 22;
    private const float GlintHalfWidth = 0.34f;
    private const float GlintTravel = 1.5f;
    private const float StatusRingShare = 0.034f;
    private const float StatusGlowShare = 0.085f;
    private const float StatusBlurSigma = 6;
    private const float StatusClearShare = 0.6f;
    private const byte StatusRingAlpha = 235;
    private const byte StatusGlowAlpha = 150;
    private const byte StatusTintAlpha = 46;
    private const byte FlashAlpha = 150;
    private const byte GlintAlpha = 165;

    private static readonly SKColor FlashColor = SKColor.Parse("#E6FCFF");

    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _rim = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _glow = new() { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
    private readonly SKPaint _drop = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _status = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _overlay = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKMaskFilter _statusBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, StatusBlurSigma);

    public void Draw(SKCanvas canvas, float radius, float t, float travelAngle, float speed, float alpha)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var wobble = MathF.Sin(t * 13) * (0.025f + 0.03f * speed);
        var stretch = 0.1f * speed + wobble;
        canvas.Save();
        canvas.RotateDegrees(travelAngle);
        canvas.Scale(1 + stretch, 1 - stretch * 0.85f);
        canvas.RotateDegrees(-travelAngle);
        var a = (byte)(255 * alpha);
        using (var shader = SKShader.CreateRadialGradient(SKPoint.Empty, radius, [SKColor.Parse("#5CD6EE").WithAlpha(0), SKColor.Parse("#7FE9FF").WithAlpha(14), SKColor.Parse("#D9FBFF").WithAlpha(70)], [0, 0.72f, 1], SKShaderTileMode.Clamp))
        {
            _fill.Shader = shader;
            _fill.Color = SKColors.White.WithAlpha(a);
            canvas.DrawCircle(0, 0, radius, _fill);
            _fill.Shader = null;
        }
        var rimWidth = MathF.Max(1.6f, radius * 0.016f);
        SKColor[] hues = [SKColor.Parse("#7FE9FF").WithAlpha(190), SKColor.Parse("#C8A8FF").WithAlpha(170), SKColor.Parse("#FFD3F0").WithAlpha(170), SKColor.Parse("#A8FFE0").WithAlpha(180), SKColor.Parse("#7FE9FF").WithAlpha(190)];
        using (var shader = SKShader.CreateSweepGradient(SKPoint.Empty, hues, [0, 0.25f, 0.5f, 0.75f, 1]))
        {
            canvas.Save();
            canvas.RotateDegrees(t * 70);
            _rim.Shader = shader;
            _rim.StrokeWidth = rimWidth;
            _rim.Color = SKColors.White.WithAlpha(a);
            canvas.DrawCircle(0, 0, radius - rimWidth / 2, _rim);
            _rim.StrokeWidth = rimWidth * 0.6f;
            canvas.DrawCircle(wobble * radius * 0.5f, 0, radius * 0.94f, _rim);
            _rim.Shader = null;
            canvas.Restore();
        }
        var inner = new SKRect(-radius * 0.8f, -radius * 0.8f, radius * 0.8f, radius * 0.8f);
        _glow.StrokeWidth = MathF.Max(2, radius * 0.04f);
        _glow.Color = SKColors.White.WithAlpha((byte)(205 * alpha));
        canvas.DrawArc(inner, 200, 52, false, _glow);
        _glow.StrokeWidth = MathF.Max(1.4f, radius * 0.022f);
        _glow.Color = SKColors.White.WithAlpha((byte)(95 * alpha));
        canvas.DrawArc(inner, 20, 34, false, _glow);
        canvas.Restore();
    }

    public void DrawPop(SKCanvas canvas, float x, float y, float radius, float q)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var fade = 1 - q;
        for (var i = 0; i < 14; i++)
        {
            var angle = i * MathF.PI * 2 / 14 + 0.2f;
            var reach = 16 + 10 * (i % 3);
            var distance = radius * 1.04f + reach * OutCubic(q);
            _drop.Color = (i % 2 == 0 ? SKColors.White : SKColor.Parse("#9CEBFA")).WithAlpha((byte)(230 * fade));
            canvas.DrawCircle(x + MathF.Cos(angle) * distance, y + MathF.Sin(angle) * distance, MathF.Max(0.5f, (2.6f - (i % 3) * 0.6f) * (1 - 0.6f * q)), _drop);
        }
        var splash = Math.Clamp(q * 1.15f, 0, 1);
        for (var i = 0; i < 7; i++)
        {
            var vx = (i - 3) * 9f;
            var vy = -34f - (i % 3) * 12f;
            var px = x + vx * splash;
            var py = y + vy * splash + 70 * splash * splash;
            _drop.Color = SKColors.White.WithAlpha((byte)(210 * (1 - splash)));
            canvas.DrawCircle(px, py, 1.7f * (1 - 0.5f * splash), _drop);
        }
        _rim.Shader = null;
        _rim.Color = SKColor.Parse("#BDF6FF").WithAlpha((byte)(150 * fade));
        _rim.StrokeWidth = 1.3f;
        var spread = 8 + 26 * OutCubic(q);
        canvas.DrawOval(new SKRect(x - spread, y + 10 - spread * 0.3f, x + spread, y + 10 + spread * 0.3f), _rim);
    }

    public void DrawStatus(SKCanvas canvas, float radius, SKColor color, bool lit)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        var ring = MathF.Max(2.5f, radius * StatusRingShare);
        using (var shader = SKShader.CreateRadialGradient(SKPoint.Empty, radius, [color.WithAlpha(0), color.WithAlpha(StatusTintAlpha)], [StatusClearShare, 1], SKShaderTileMode.Clamp))
        {
            _fill.Shader = shader;
            _fill.Color = SKColors.White;
            canvas.DrawCircle(0, 0, radius, _fill);
            _fill.Shader = null;
        }

        if (lit)
        {
            _status.MaskFilter = _statusBlur;
            _status.StrokeWidth = radius * StatusGlowShare;
            _status.Color = color.WithAlpha(StatusGlowAlpha);
            canvas.DrawCircle(0, 0, radius, _status);
            _status.MaskFilter = null;
        }

        _status.StrokeWidth = ring;
        _status.Color = color.WithAlpha(lit ? StatusRingAlpha : (byte)(StatusRingAlpha / 2));
        canvas.DrawCircle(0, 0, radius - ring / 2, _status);
    }

    public void DrawGlint(SKCanvas canvas, float radius, float progress)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (progress <= 0 || progress >= 1)
        {
            return;
        }

        var x = -radius * GlintTravel + radius * GlintTravel * 2 * OutCubic(progress);
        var half = radius * GlintHalfWidth;
        using var clip = new SKPath();
        clip.AddCircle(0, 0, radius * 0.97f);
        canvas.Save();
        canvas.ClipPath(clip, SKClipOperation.Intersect, true);
        canvas.RotateDegrees(GlintTilt);
        using var shader = SKShader.CreateLinearGradient(new SKPoint(x - half, 0), new SKPoint(x + half, 0), [SKColors.White.WithAlpha(0), SKColors.White.WithAlpha(GlintAlpha), SKColors.White.WithAlpha(0)], [0, 0.5f, 1], SKShaderTileMode.Clamp);
        _overlay.Shader = shader;
        _overlay.Color = SKColors.White;
        canvas.DrawRect(x - half, -radius * 2, half * 2, radius * 4, _overlay);
        _overlay.Shader = null;
        canvas.Restore();
    }

    public void DrawFlash(SKCanvas canvas, float radius, float strength)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        if (strength <= 0)
        {
            return;
        }

        _overlay.Shader = null;
        _overlay.Color = FlashColor.WithAlpha((byte)(FlashAlpha * Math.Clamp(strength, 0, 1)));
        canvas.DrawCircle(0, 0, radius, _overlay);
    }

    public void Dispose()
    {
        _fill.Dispose();
        _rim.Dispose();
        _glow.Dispose();
        _drop.Dispose();
        _status.Dispose();
        _overlay.Dispose();
        _statusBlur.Dispose();
    }

    private static float OutCubic(float x) => 1 - MathF.Pow(1 - Math.Clamp(x, 0, 1), 3);
}
