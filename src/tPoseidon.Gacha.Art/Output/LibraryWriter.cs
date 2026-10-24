using System.Globalization;
using System.Text;
using System.Text.Json;
using tPoseidon.Gacha.Art.Swf;

namespace tPoseidon.Gacha.Art.Output;

public sealed record BitmapEntry(int Id, string File, int Width, int Height, bool Alpha);

public sealed record SoundEntry(int Id, string File, int Rate, int Channels, uint Samples);

public sealed record SkippedEntry(int Id, string Kind, string Reason);

public sealed class LibraryWriter
{
    public const int FormatVersion = 1;
    private const double TwipsPerPixel = 20.0;

    private readonly SwfLibrary _library;
    private readonly IReadOnlyList<BitmapEntry> _bitmaps;
    private readonly IReadOnlyList<SoundEntry> _sounds;
    private readonly IReadOnlyList<SkippedEntry> _skipped;

    public LibraryWriter(SwfLibrary library, IReadOnlyList<BitmapEntry> bitmaps, IReadOnlyList<SoundEntry> sounds, IReadOnlyList<SkippedEntry> skipped)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
        _bitmaps = bitmaps ?? throw new ArgumentNullException(nameof(bitmaps));
        _sounds = sounds ?? throw new ArgumentNullException(nameof(sounds));
        _skipped = skipped ?? throw new ArgumentNullException(nameof(skipped));
    }

    public byte[] Write()
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            json.WriteStartObject();
            json.WriteNumber("format", FormatVersion);
            WriteStage(json);
            json.WriteStartObject("exports");
            foreach (var (name, id) in _library.Exports.OrderBy(e => e.Value)) json.WriteNumber(name, id);
            json.WriteEndObject();
            WriteBitmaps(json);
            WriteSounds(json);
            WriteSkipped(json);
            WriteFonts(json);
            json.WriteStartObject("characters");
            foreach (var shape in _library.Shapes.Values.OrderBy(s => s.Id)) WriteShape(json, shape);
            foreach (var sprite in _library.Sprites.Values.OrderBy(s => s.Id))
            {
                json.WriteStartObject(Key(sprite.Id));
                json.WriteString("type", "sprite");
                WriteTimeline(json, sprite);
                json.WriteEndObject();
            }
            foreach (var button in _library.Buttons.Values.OrderBy(b => b.Id)) WriteButton(json, button);
            foreach (var text in _library.Texts.Values.OrderBy(t => t.Id)) WriteText(json, text);
            foreach (var edit in _library.EditTexts.Values.OrderBy(t => t.Id)) WriteEditText(json, edit);
            json.WriteEndObject();
            json.WriteStartObject("root");
            WriteTimeline(json, _library.Root);
            json.WriteEndObject();
            json.WriteEndObject();
        }
        return stream.ToArray();
    }

    private void WriteStage(Utf8JsonWriter json)
    {
        var file = _library.File;
        json.WriteStartObject("stage");
        json.WriteNumber("version", file.Version);
        json.WriteNumber("x", Px(file.FrameSize.XMin));
        json.WriteNumber("y", Px(file.FrameSize.YMin));
        json.WriteNumber("width", Px(file.FrameSize.XMax - file.FrameSize.XMin));
        json.WriteNumber("height", Px(file.FrameSize.YMax - file.FrameSize.YMin));
        json.WriteNumber("frameRate", file.FrameRate);
        json.WriteNumber("frameCount", file.FrameCount);
        json.WritePropertyName("background");
        WriteColor(json, _library.Background);
        json.WriteEndObject();
    }

    private void WriteBitmaps(Utf8JsonWriter json)
    {
        json.WriteStartObject("bitmaps");
        foreach (var bitmap in _bitmaps.OrderBy(b => b.Id))
        {
            json.WriteStartObject(Key(bitmap.Id));
            json.WriteString("file", bitmap.File);
            json.WriteNumber("width", bitmap.Width);
            json.WriteNumber("height", bitmap.Height);
            json.WriteBoolean("alpha", bitmap.Alpha);
            json.WriteEndObject();
        }
        json.WriteEndObject();
    }

    private void WriteSounds(Utf8JsonWriter json)
    {
        json.WriteStartObject("sounds");
        foreach (var sound in _sounds.OrderBy(s => s.Id))
        {
            json.WriteStartObject(Key(sound.Id));
            json.WriteString("file", sound.File);
            json.WriteNumber("rate", sound.Rate);
            json.WriteNumber("channels", sound.Channels);
            json.WriteNumber("samples", sound.Samples);
            json.WriteNumber("duration", sound.Samples / (double)sound.Rate);
            json.WriteEndObject();
        }
        json.WriteEndObject();
    }

    private void WriteSkipped(Utf8JsonWriter json)
    {
        json.WriteStartArray("skipped");
        foreach (var skipped in _skipped)
        {
            json.WriteStartObject();
            json.WriteNumber("id", skipped.Id);
            json.WriteString("kind", skipped.Kind);
            json.WriteString("reason", skipped.Reason);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private void WriteFonts(Utf8JsonWriter json)
    {
        json.WriteStartObject("fonts");
        foreach (var font in _library.Fonts.Values.OrderBy(f => f.Id))
        {
            json.WriteStartObject(Key(font.Id));
            json.WriteString("name", font.Name);
            json.WriteBoolean("bold", font.Bold);
            json.WriteBoolean("italic", font.Italic);
            json.WriteNumber("glyphs", font.GlyphCount);
            json.WriteEndObject();
        }
        json.WriteEndObject();
    }

    private static void WriteShape(Utf8JsonWriter json, ShapeDefinition shape)
    {
        json.WriteStartObject(Key(shape.Id));
        json.WriteString("type", shape.IsMorph ? "morph" : "shape");
        json.WritePropertyName("bounds");
        WriteRect(json, shape.Bounds);
        if (shape.EndBounds is { } end)
        {
            json.WritePropertyName("endBounds");
            WriteRect(json, end);
        }
        json.WriteStartArray("layers");
        foreach (var layer in shape.Layers)
        {
            json.WriteStartObject();
            json.WriteStartArray("fills");
            foreach (var pair in layer.Styles.Fills)
            {
                if (shape.IsMorph)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("start");
                    WriteFill(json, pair.Start);
                    json.WritePropertyName("end");
                    WriteFill(json, pair.End);
                    json.WriteEndObject();
                }
                else
                {
                    WriteFill(json, pair.Start);
                }
            }
            json.WriteEndArray();
            json.WriteStartArray("lines");
            foreach (var pair in layer.Styles.Lines)
            {
                if (shape.IsMorph)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("start");
                    WriteLine(json, pair.Start);
                    json.WritePropertyName("end");
                    WriteLine(json, pair.End);
                    json.WriteEndObject();
                }
                else
                {
                    WriteLine(json, pair.Start);
                }
            }
            json.WriteEndArray();
            WritePaths(json, "fillPaths", layer.Fills, shape.IsMorph);
            WritePaths(json, "linePaths", layer.Lines, shape.IsMorph);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WritePaths(Utf8JsonWriter json, string name, IReadOnlyList<StyledPath> paths, bool morph)
    {
        json.WriteStartArray(name);
        foreach (var path in paths)
        {
            json.WriteStartObject();
            json.WriteNumber("style", path.Style);
            json.WriteString("d", PathData(path.Commands, false));
            if (morph) json.WriteString("e", PathData(path.Commands, true));
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    public static string PathData(IReadOnlyList<PathCommand> commands, bool end)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var text = new StringBuilder();
        foreach (var command in commands)
        {
            switch (command.Verb)
            {
                case PathVerb.Move:
                    text.Append('M');
                    AppendPoint(text, command.Point, end);
                    break;
                case PathVerb.Line:
                    text.Append('L');
                    AppendPoint(text, command.Point, end);
                    break;
                case PathVerb.Curve:
                    text.Append('Q');
                    AppendPoint(text, command.Control, end);
                    text.Append(' ');
                    AppendPoint(text, command.Point, end);
                    break;
                case PathVerb.Close:
                    text.Append('Z');
                    break;
                default:
                    throw new InvalidOperationException($"unknown path verb {command.Verb}");
            }
        }
        return text.ToString();
    }

    private static void AppendPoint(StringBuilder text, MorphPoint point, bool end)
    {
        text.Append(PxText(end ? point.EndX : point.X));
        text.Append(' ');
        text.Append(PxText(end ? point.EndY : point.Y));
    }

    private static void WriteFill(Utf8JsonWriter json, FillStyle fill)
    {
        json.WriteStartObject();
        switch (fill)
        {
            case SolidFill solid:
                json.WriteString("type", "solid");
                json.WritePropertyName("color");
                WriteColor(json, solid.Color);
                break;
            case GradientFill gradient:
                json.WriteString("type", gradient.Kind switch
                {
                    GradientKind.Linear => "linear",
                    GradientKind.Radial => "radial",
                    _ => "focal",
                });
                json.WritePropertyName("matrix");
                WriteScaledMatrix(json, gradient.Matrix);
                json.WriteNumber("spread", gradient.Spread);
                json.WriteNumber("interpolation", gradient.Interpolation);
                json.WriteNumber("focal", gradient.Focal);
                json.WriteStartArray("stops");
                foreach (var stop in gradient.Stops)
                {
                    json.WriteStartArray();
                    json.WriteNumberValue(stop.Ratio);
                    json.WriteNumberValue(stop.Color.R);
                    json.WriteNumberValue(stop.Color.G);
                    json.WriteNumberValue(stop.Color.B);
                    json.WriteNumberValue(stop.Color.A);
                    json.WriteEndArray();
                }
                json.WriteEndArray();
                break;
            case BitmapFill bitmap:
                json.WriteString("type", "bitmap");
                json.WriteNumber("bitmap", bitmap.BitmapId);
                json.WritePropertyName("matrix");
                WriteScaledMatrix(json, bitmap.Matrix);
                json.WriteBoolean("repeat", bitmap.Repeat);
                json.WriteBoolean("smooth", bitmap.Smooth);
                break;
            default:
                throw new InvalidOperationException($"unknown fill {fill.GetType().Name}");
        }
        json.WriteEndObject();
    }

    private static void WriteLine(Utf8JsonWriter json, LineStyle line)
    {
        json.WriteStartObject();
        json.WriteNumber("width", Px(line.Width));
        if (line.Fill is { } fill)
        {
            json.WritePropertyName("fill");
            WriteFill(json, fill);
        }
        else
        {
            json.WritePropertyName("color");
            WriteColor(json, line.Color);
        }
        json.WriteNumber("startCap", line.StartCap);
        json.WriteNumber("endCap", line.EndCap);
        json.WriteNumber("join", line.Join);
        json.WriteNumber("miter", line.MiterLimit);
        if (line.NoHorizontalScale) json.WriteBoolean("noHScale", true);
        if (line.NoVerticalScale) json.WriteBoolean("noVScale", true);
        if (line.NoClose) json.WriteBoolean("noClose", true);
        json.WriteEndObject();
    }

    private static void WriteButton(Utf8JsonWriter json, ButtonDefinition button)
    {
        json.WriteStartObject(Key(button.Id));
        json.WriteString("type", "button");
        json.WriteBoolean("trackAsMenu", button.TrackAsMenu);
        json.WriteStartArray("records");
        foreach (var record in button.Records)
        {
            json.WriteStartObject();
            json.WriteStartArray("states");
            if (record.Up) json.WriteStringValue("up");
            if (record.Over) json.WriteStringValue("over");
            if (record.Down) json.WriteStringValue("down");
            if (record.Hit) json.WriteStringValue("hit");
            json.WriteEndArray();
            json.WriteNumber("id", record.CharacterId);
            json.WriteNumber("depth", record.Depth);
            json.WritePropertyName("matrix");
            WritePlacementMatrix(json, record.Matrix);
            if (!record.ColorTransform.IsIdentity)
            {
                json.WritePropertyName("colorTransform");
                WriteColorTransform(json, record.ColorTransform);
            }
            if (record.BlendMode is { } blend) json.WriteNumber("blend", blend);
            if (record.Filters is { } filters) WriteFilters(json, filters);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteStartArray("conditions");
        foreach (var condition in button.ActionConditions) json.WriteStringValue(condition);
        json.WriteEndArray();
        json.WriteStartObject("sounds");
        foreach (var (name, sound) in button.Sounds)
        {
            json.WritePropertyName(name);
            WriteSound(json, sound);
        }
        json.WriteEndObject();
        json.WriteEndObject();
    }

    private static void WriteText(Utf8JsonWriter json, TextDefinition text)
    {
        json.WriteStartObject(Key(text.Id));
        json.WriteString("type", "text");
        json.WritePropertyName("bounds");
        WriteRect(json, text.Bounds);
        json.WritePropertyName("matrix");
        WritePlacementMatrix(json, text.Matrix);
        json.WriteStartArray("runs");
        foreach (var run in text.Runs)
        {
            json.WriteStartObject();
            if (run.FontId is { } font) json.WriteNumber("font", font);
            if (run.Height is { } height) json.WriteNumber("height", Px(height));
            if (run.Color is { } color)
            {
                json.WritePropertyName("color");
                WriteColor(json, color);
            }
            if (run.X is { } x) json.WriteNumber("x", Px(x));
            if (run.Y is { } y) json.WriteNumber("y", Px(y));
            json.WriteStartArray("advances");
            foreach (var glyph in run.Glyphs) json.WriteNumberValue(Px(glyph.Advance));
            json.WriteEndArray();
            json.WriteString("text", run.Text);
            json.WriteEndObject();
        }
        json.WriteEndArray();
        json.WriteEndObject();
    }

    private static void WriteEditText(Utf8JsonWriter json, EditTextDefinition edit)
    {
        json.WriteStartObject(Key(edit.Id));
        json.WriteString("type", "edittext");
        json.WritePropertyName("bounds");
        WriteRect(json, edit.Bounds);
        json.WriteString("variable", edit.VariableName);
        if (edit.InitialText is { } initial) json.WriteString("text", initial);
        if (edit.FontId is { } font) json.WriteNumber("font", font);
        if (edit.FontHeight is { } height) json.WriteNumber("height", Px(height));
        if (edit.Color is { } color)
        {
            json.WritePropertyName("color");
            WriteColor(json, color);
        }
        if (edit.MaxLength is { } max) json.WriteNumber("maxLength", max);
        if (edit.Align is { } align)
        {
            json.WriteNumber("align", align);
            json.WriteNumber("leftMargin", Px(edit.LeftMargin ?? 0));
            json.WriteNumber("rightMargin", Px(edit.RightMargin ?? 0));
            json.WriteNumber("indent", Px(edit.Indent ?? 0));
            json.WriteNumber("leading", Px(edit.Leading ?? 0));
        }
        json.WriteBoolean("wordWrap", edit.WordWrap);
        json.WriteBoolean("multiline", edit.Multiline);
        json.WriteBoolean("password", edit.Password);
        json.WriteBoolean("readOnly", edit.ReadOnly);
        json.WriteBoolean("autoSize", edit.AutoSize);
        json.WriteBoolean("noSelect", edit.NoSelect);
        json.WriteBoolean("border", edit.Border);
        json.WriteBoolean("html", edit.Html);
        json.WriteBoolean("useOutlines", edit.UseOutlines);
        json.WriteEndObject();
    }

    private static void WriteTimeline(Utf8JsonWriter json, SpriteDefinition sprite)
    {
        json.WriteNumber("frameCount", sprite.Frames.Count);
        json.WriteStartArray("frames");
        foreach (var frame in sprite.Frames)
        {
            json.WriteStartObject();
            if (frame.Labels.Count > 0)
            {
                json.WriteStartArray("labels");
                foreach (var label in frame.Labels) json.WriteStringValue(label);
                json.WriteEndArray();
            }
            if (frame.Operations.Count > 0)
            {
                json.WriteStartArray("ops");
                foreach (var operation in frame.Operations) WriteOperation(json, operation);
                json.WriteEndArray();
            }
            if (frame.Sounds.Count > 0)
            {
                json.WriteStartArray("sounds");
                foreach (var sound in frame.Sounds) WriteSound(json, sound);
                json.WriteEndArray();
            }
            if (frame.HasActions) json.WriteBoolean("actions", true);
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteOperation(Utf8JsonWriter json, DisplayOperation operation)
    {
        json.WriteStartObject();
        switch (operation)
        {
            case RemoveOperation remove:
                json.WriteString("op", "remove");
                json.WriteNumber("depth", remove.Depth);
                break;
            case PlaceOperation place:
                json.WriteString("op", "place");
                json.WriteNumber("depth", place.Depth);
                if (place.Move) json.WriteBoolean("move", true);
                if (place.CharacterId is { } id) json.WriteNumber("id", id);
                if (place.Matrix is { } matrix)
                {
                    json.WritePropertyName("matrix");
                    WritePlacementMatrix(json, matrix);
                }
                if (place.ColorTransform is { } colorTransform)
                {
                    json.WritePropertyName("colorTransform");
                    WriteColorTransform(json, colorTransform);
                }
                if (place.Ratio is { } ratio) json.WriteNumber("ratio", ratio);
                if (place.Name is { } name) json.WriteString("name", name);
                if (place.ClipDepth is { } clip) json.WriteNumber("clipDepth", clip);
                if (place.BlendMode is { } blend) json.WriteNumber("blend", blend);
                if (place.CacheAsBitmap is { } cache) json.WriteBoolean("cacheAsBitmap", cache);
                if (place.Filters is { } filters) WriteFilters(json, filters);
                if (place.ClipEvents.Count > 0)
                {
                    json.WriteStartArray("events");
                    foreach (var clipEvent in place.ClipEvents) json.WriteStringValue(clipEvent);
                    json.WriteEndArray();
                }
                break;
            default:
                throw new InvalidOperationException($"unknown display operation {operation.GetType().Name}");
        }
        json.WriteEndObject();
    }

    private static void WriteSound(Utf8JsonWriter json, SoundStart sound)
    {
        json.WriteStartObject();
        json.WriteNumber("id", sound.SoundId);
        if (sound.Info.SyncStop) json.WriteBoolean("stop", true);
        if (sound.Info.SyncNoMultiple) json.WriteBoolean("noMultiple", true);
        if (sound.Info.Loops is { } loops) json.WriteNumber("loops", loops);
        if (sound.Info.InPoint is { } inPoint) json.WriteNumber("in", inPoint);
        if (sound.Info.OutPoint is { } outPoint) json.WriteNumber("out", outPoint);
        json.WriteEndObject();
    }

    private static void WriteFilters(Utf8JsonWriter json, IReadOnlyList<SwfFilter> filters)
    {
        json.WriteStartArray("filters");
        foreach (var filter in filters)
        {
            json.WriteStartObject();
            switch (filter)
            {
                case DropShadowFilter shadow:
                    json.WriteString("type", "dropShadow");
                    json.WritePropertyName("color");
                    WriteColor(json, shadow.Color);
                    json.WriteNumber("blurX", shadow.BlurX);
                    json.WriteNumber("blurY", shadow.BlurY);
                    json.WriteNumber("angle", shadow.Angle);
                    json.WriteNumber("distance", shadow.Distance);
                    json.WriteNumber("strength", shadow.Strength);
                    json.WriteBoolean("inner", shadow.Inner);
                    json.WriteBoolean("knockout", shadow.Knockout);
                    json.WriteNumber("passes", shadow.Passes);
                    break;
                case BlurFilter blur:
                    json.WriteString("type", "blur");
                    json.WriteNumber("blurX", blur.BlurX);
                    json.WriteNumber("blurY", blur.BlurY);
                    json.WriteNumber("passes", blur.Passes);
                    break;
                case GlowFilter glow:
                    json.WriteString("type", "glow");
                    json.WritePropertyName("color");
                    WriteColor(json, glow.Color);
                    json.WriteNumber("blurX", glow.BlurX);
                    json.WriteNumber("blurY", glow.BlurY);
                    json.WriteNumber("strength", glow.Strength);
                    json.WriteBoolean("inner", glow.Inner);
                    json.WriteBoolean("knockout", glow.Knockout);
                    json.WriteNumber("passes", glow.Passes);
                    break;
                case BevelFilter bevel:
                    json.WriteString("type", "bevel");
                    json.WritePropertyName("shadow");
                    WriteColor(json, bevel.Shadow);
                    json.WritePropertyName("highlight");
                    WriteColor(json, bevel.Highlight);
                    json.WriteNumber("blurX", bevel.BlurX);
                    json.WriteNumber("blurY", bevel.BlurY);
                    json.WriteNumber("angle", bevel.Angle);
                    json.WriteNumber("distance", bevel.Distance);
                    json.WriteNumber("strength", bevel.Strength);
                    json.WriteBoolean("inner", bevel.Inner);
                    json.WriteBoolean("knockout", bevel.Knockout);
                    json.WriteBoolean("onTop", bevel.OnTop);
                    json.WriteNumber("passes", bevel.Passes);
                    break;
                case GradientGlowFilter gradient:
                    json.WriteString("type", gradient.Bevel ? "gradientBevel" : "gradientGlow");
                    json.WriteStartArray("stops");
                    foreach (var stop in gradient.Stops)
                    {
                        json.WriteStartArray();
                        json.WriteNumberValue(stop.Ratio);
                        json.WriteNumberValue(stop.Color.R);
                        json.WriteNumberValue(stop.Color.G);
                        json.WriteNumberValue(stop.Color.B);
                        json.WriteNumberValue(stop.Color.A);
                        json.WriteEndArray();
                    }
                    json.WriteEndArray();
                    json.WriteNumber("blurX", gradient.BlurX);
                    json.WriteNumber("blurY", gradient.BlurY);
                    json.WriteNumber("angle", gradient.Angle);
                    json.WriteNumber("distance", gradient.Distance);
                    json.WriteNumber("strength", gradient.Strength);
                    json.WriteBoolean("inner", gradient.Inner);
                    json.WriteBoolean("knockout", gradient.Knockout);
                    json.WriteBoolean("onTop", gradient.OnTop);
                    json.WriteNumber("passes", gradient.Passes);
                    break;
                case ConvolutionFilter convolution:
                    json.WriteString("type", "convolution");
                    json.WriteNumber("columns", convolution.Columns);
                    json.WriteNumber("rows", convolution.Rows);
                    json.WriteNumber("divisor", convolution.Divisor);
                    json.WriteNumber("bias", convolution.Bias);
                    json.WriteStartArray("matrix");
                    foreach (var value in convolution.Matrix) json.WriteNumberValue(value);
                    json.WriteEndArray();
                    json.WritePropertyName("color");
                    WriteColor(json, convolution.Default);
                    json.WriteBoolean("clamp", convolution.Clamp);
                    json.WriteBoolean("preserveAlpha", convolution.PreserveAlpha);
                    break;
                case ColorMatrixFilter colorMatrix:
                    json.WriteString("type", "colorMatrix");
                    json.WriteStartArray("matrix");
                    foreach (var value in colorMatrix.Matrix) json.WriteNumberValue(value);
                    json.WriteEndArray();
                    break;
                default:
                    throw new InvalidOperationException($"unknown filter {filter.GetType().Name}");
            }
            json.WriteEndObject();
        }
        json.WriteEndArray();
    }

    private static void WriteRect(Utf8JsonWriter json, SwfRect rect)
    {
        json.WriteStartArray();
        json.WriteNumberValue(Px(rect.XMin));
        json.WriteNumberValue(Px(rect.YMin));
        json.WriteNumberValue(Px(rect.XMax));
        json.WriteNumberValue(Px(rect.YMax));
        json.WriteEndArray();
    }

    private static void WriteColor(Utf8JsonWriter json, SwfColor color)
    {
        json.WriteStartArray();
        json.WriteNumberValue(color.R);
        json.WriteNumberValue(color.G);
        json.WriteNumberValue(color.B);
        json.WriteNumberValue(color.A);
        json.WriteEndArray();
    }

    private static void WritePlacementMatrix(Utf8JsonWriter json, SwfMatrix matrix)
    {
        json.WriteStartArray();
        json.WriteNumberValue(matrix.A);
        json.WriteNumberValue(matrix.B);
        json.WriteNumberValue(matrix.C);
        json.WriteNumberValue(matrix.D);
        json.WriteNumberValue(Px(matrix.TranslateX));
        json.WriteNumberValue(Px(matrix.TranslateY));
        json.WriteEndArray();
    }

    private static void WriteScaledMatrix(Utf8JsonWriter json, SwfMatrix matrix)
    {
        json.WriteStartArray();
        json.WriteNumberValue(matrix.A / TwipsPerPixel);
        json.WriteNumberValue(matrix.B / TwipsPerPixel);
        json.WriteNumberValue(matrix.C / TwipsPerPixel);
        json.WriteNumberValue(matrix.D / TwipsPerPixel);
        json.WriteNumberValue(Px(matrix.TranslateX));
        json.WriteNumberValue(Px(matrix.TranslateY));
        json.WriteEndArray();
    }

    private static void WriteColorTransform(Utf8JsonWriter json, SwfColorTransform transform)
    {
        json.WriteStartArray();
        json.WriteNumberValue(transform.RedMultiplier);
        json.WriteNumberValue(transform.GreenMultiplier);
        json.WriteNumberValue(transform.BlueMultiplier);
        json.WriteNumberValue(transform.AlphaMultiplier);
        json.WriteNumberValue(transform.RedAdd);
        json.WriteNumberValue(transform.GreenAdd);
        json.WriteNumberValue(transform.BlueAdd);
        json.WriteNumberValue(transform.AlphaAdd);
        json.WriteEndArray();
    }

    private static double Px(int twips) => twips / TwipsPerPixel;

    private static string PxText(int twips) => (twips / TwipsPerPixel).ToString("0.##", CultureInfo.InvariantCulture);

    private static string Key(int id) => id.ToString(CultureInfo.InvariantCulture);
}
