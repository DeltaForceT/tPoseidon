namespace tPoseidon.Gacha.Art.Swf;

public enum GradientKind
{
    Linear,
    Radial,
    FocalRadial,
}

public readonly record struct GradientStop(byte Ratio, SwfColor Color);

public abstract record FillStyle;

public sealed record SolidFill(SwfColor Color) : FillStyle;

public sealed record GradientFill(GradientKind Kind, SwfMatrix Matrix, int Spread, int Interpolation, IReadOnlyList<GradientStop> Stops, double Focal) : FillStyle;

public sealed record BitmapFill(int BitmapId, SwfMatrix Matrix, bool Repeat, bool Smooth) : FillStyle;

public sealed record LineStyle(int Width, SwfColor Color, FillStyle? Fill, int StartCap, int EndCap, int Join, double MiterLimit, bool NoHorizontalScale, bool NoVerticalScale, bool PixelHinting, bool NoClose);

public sealed record StylePair<T>(T Start, T End)
    where T : class;

public sealed record StyleSet(IReadOnlyList<StylePair<FillStyle>> Fills, IReadOnlyList<StylePair<LineStyle>> Lines, bool IsMorph);

public static class ShapeStyleReader
{
    public const int SolidType = 0x00;
    public const int LinearGradientType = 0x10;
    public const int RadialGradientType = 0x12;
    public const int FocalRadialGradientType = 0x13;
    public const int RepeatingBitmapType = 0x40;
    public const int ClippedBitmapType = 0x41;
    public const int RepeatingHardBitmapType = 0x42;
    public const int ClippedHardBitmapType = 0x43;

    public static StyleSet ReadShapeStyles(SwfReader reader, int shapeVersion)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var fills = new List<StylePair<FillStyle>>();
        var fillCount = ReadCount(reader, shapeVersion);
        for (var i = 0; i < fillCount; i++)
        {
            var fill = ReadFill(reader, shapeVersion);
            fills.Add(new StylePair<FillStyle>(fill, fill));
        }
        var lines = new List<StylePair<LineStyle>>();
        var lineCount = ReadCount(reader, shapeVersion);
        for (var i = 0; i < lineCount; i++)
        {
            var line = ReadLine(reader, shapeVersion);
            lines.Add(new StylePair<LineStyle>(line, line));
        }
        return new StyleSet(fills, lines, false);
    }

    public static StyleSet ReadMorphStyles(SwfReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var fills = new List<StylePair<FillStyle>>();
        var fillCount = ReadCount(reader, 2);
        for (var i = 0; i < fillCount; i++) fills.Add(ReadMorphFill(reader));
        var lines = new List<StylePair<LineStyle>>();
        var lineCount = ReadCount(reader, 2);
        for (var i = 0; i < lineCount; i++)
        {
            var startWidth = reader.U16();
            var endWidth = reader.U16();
            var startColor = SwfColor.ReadRgba(reader);
            var endColor = SwfColor.ReadRgba(reader);
            lines.Add(new StylePair<LineStyle>(SimpleLine(startWidth, startColor), SimpleLine(endWidth, endColor)));
        }
        return new StyleSet(fills, lines, true);
    }

    public static FillStyle ReadFill(SwfReader reader, int shapeVersion)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var type = reader.U8();
        switch (type)
        {
            case SolidType:
                return new SolidFill(shapeVersion >= 3 ? SwfColor.ReadRgba(reader) : SwfColor.ReadRgb(reader));
            case LinearGradientType:
            case RadialGradientType:
            case FocalRadialGradientType:
            {
                var matrix = SwfMatrix.Read(reader);
                var flags = reader.U8();
                var spread = (flags >> 6) & 3;
                var interpolation = (flags >> 4) & 3;
                var count = flags & 0x0F;
                var stops = new List<GradientStop>(count);
                for (var i = 0; i < count; i++)
                {
                    var ratio = reader.U8();
                    stops.Add(new GradientStop(ratio, shapeVersion >= 3 ? SwfColor.ReadRgba(reader) : SwfColor.ReadRgb(reader)));
                }
                var focal = type == FocalRadialGradientType ? reader.Fixed8() : 0;
                return new GradientFill(KindOf(type), matrix, spread, interpolation, stops, focal);
            }
            case RepeatingBitmapType:
            case ClippedBitmapType:
            case RepeatingHardBitmapType:
            case ClippedHardBitmapType:
            {
                var bitmap = reader.U16();
                var matrix = SwfMatrix.Read(reader);
                return new BitmapFill(bitmap, matrix, type is RepeatingBitmapType or RepeatingHardBitmapType, type is RepeatingBitmapType or ClippedBitmapType);
            }
            default:
                throw new SwfFormatException($"unknown fill style type 0x{type:X2}");
        }
    }

    private static StylePair<FillStyle> ReadMorphFill(SwfReader reader)
    {
        var type = reader.U8();
        switch (type)
        {
            case SolidType:
                return new StylePair<FillStyle>(new SolidFill(SwfColor.ReadRgba(reader)), new SolidFill(SwfColor.ReadRgba(reader)));
            case LinearGradientType:
            case RadialGradientType:
            case FocalRadialGradientType:
            {
                var startMatrix = SwfMatrix.Read(reader);
                var endMatrix = SwfMatrix.Read(reader);
                var flags = reader.U8();
                var spread = (flags >> 6) & 3;
                var interpolation = (flags >> 4) & 3;
                var count = flags & 0x0F;
                var startStops = new List<GradientStop>(count);
                var endStops = new List<GradientStop>(count);
                for (var i = 0; i < count; i++)
                {
                    startStops.Add(new GradientStop(reader.U8(), SwfColor.ReadRgba(reader)));
                    endStops.Add(new GradientStop(reader.U8(), SwfColor.ReadRgba(reader)));
                }
                var focal = type == FocalRadialGradientType ? reader.Fixed8() : 0;
                var endFocal = type == FocalRadialGradientType ? reader.Fixed8() : 0;
                var kind = KindOf(type);
                return new StylePair<FillStyle>(
                    new GradientFill(kind, startMatrix, spread, interpolation, startStops, focal),
                    new GradientFill(kind, endMatrix, spread, interpolation, endStops, endFocal));
            }
            case RepeatingBitmapType:
            case ClippedBitmapType:
            case RepeatingHardBitmapType:
            case ClippedHardBitmapType:
            {
                var bitmap = reader.U16();
                var startMatrix = SwfMatrix.Read(reader);
                var endMatrix = SwfMatrix.Read(reader);
                var repeat = type is RepeatingBitmapType or RepeatingHardBitmapType;
                var smooth = type is RepeatingBitmapType or ClippedBitmapType;
                return new StylePair<FillStyle>(new BitmapFill(bitmap, startMatrix, repeat, smooth), new BitmapFill(bitmap, endMatrix, repeat, smooth));
            }
            default:
                throw new SwfFormatException($"unknown morph fill style type 0x{type:X2}");
        }
    }

    private static LineStyle ReadLine(SwfReader reader, int shapeVersion)
    {
        var width = reader.U16();
        if (shapeVersion < 4)
        {
            return SimpleLine(width, shapeVersion >= 3 ? SwfColor.ReadRgba(reader) : SwfColor.ReadRgb(reader));
        }
        var startCap = (int)reader.UBits(2);
        var join = (int)reader.UBits(2);
        var hasFill = reader.Flag();
        var noHorizontal = reader.Flag();
        var noVertical = reader.Flag();
        var hinting = reader.Flag();
        reader.UBits(5);
        var noClose = reader.Flag();
        var endCap = (int)reader.UBits(2);
        reader.Align();
        var miter = join == 2 ? reader.Fixed8() : 3.0;
        SwfColor color = default;
        FillStyle? fill = null;
        if (hasFill) fill = ReadFill(reader, shapeVersion);
        else color = SwfColor.ReadRgba(reader);
        return new LineStyle(width, color, fill, startCap, endCap, join, miter, noHorizontal, noVertical, hinting, noClose);
    }

    private static LineStyle SimpleLine(int width, SwfColor color) =>
        new(width, color, null, 0, 0, 0, 3.0, false, false, false, false);

    private static int ReadCount(SwfReader reader, int shapeVersion)
    {
        int count = reader.U8();
        if (count == 0xFF && shapeVersion >= 2) count = reader.U16();
        return count;
    }

    private static GradientKind KindOf(int type) => type switch
    {
        LinearGradientType => GradientKind.Linear,
        RadialGradientType => GradientKind.Radial,
        _ => GradientKind.FocalRadial,
    };
}
