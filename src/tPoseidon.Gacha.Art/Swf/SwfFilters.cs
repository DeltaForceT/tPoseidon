namespace tPoseidon.Gacha.Art.Swf;

public abstract record SwfFilter;

public sealed record DropShadowFilter(SwfColor Color, double BlurX, double BlurY, double Angle, double Distance, double Strength, bool Inner, bool Knockout, int Passes) : SwfFilter;

public sealed record BlurFilter(double BlurX, double BlurY, int Passes) : SwfFilter;

public sealed record GlowFilter(SwfColor Color, double BlurX, double BlurY, double Strength, bool Inner, bool Knockout, int Passes) : SwfFilter;

public sealed record BevelFilter(SwfColor Shadow, SwfColor Highlight, double BlurX, double BlurY, double Angle, double Distance, double Strength, bool Inner, bool Knockout, bool OnTop, int Passes) : SwfFilter;

public sealed record GradientGlowFilter(bool Bevel, IReadOnlyList<GradientStop> Stops, double BlurX, double BlurY, double Angle, double Distance, double Strength, bool Inner, bool Knockout, bool OnTop, int Passes) : SwfFilter;

public sealed record ConvolutionFilter(int Columns, int Rows, double Divisor, double Bias, IReadOnlyList<double> Matrix, SwfColor Default, bool Clamp, bool PreserveAlpha) : SwfFilter;

public sealed record ColorMatrixFilter(IReadOnlyList<double> Matrix) : SwfFilter;

public static class SwfFilterReader
{
    private const int DropShadowId = 0;
    private const int BlurId = 1;
    private const int GlowId = 2;
    private const int BevelId = 3;
    private const int GradientGlowId = 4;
    private const int ConvolutionId = 5;
    private const int ColorMatrixId = 6;
    private const int GradientBevelId = 7;
    private const int ColorMatrixSize = 20;

    public static IReadOnlyList<SwfFilter> ReadList(SwfReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        var count = reader.U8();
        var filters = new List<SwfFilter>(count);
        for (var i = 0; i < count; i++) filters.Add(Read(reader));
        return filters;
    }

    private static SwfFilter Read(SwfReader reader)
    {
        var id = reader.U8();
        switch (id)
        {
            case DropShadowId:
            {
                var color = SwfColor.ReadRgba(reader);
                var blurX = Fixed(reader);
                var blurY = Fixed(reader);
                var angle = Fixed(reader);
                var distance = Fixed(reader);
                var strength = reader.Fixed8();
                var inner = reader.Flag();
                var knockout = reader.Flag();
                reader.Flag();
                var passes = (int)reader.UBits(5);
                reader.Align();
                return new DropShadowFilter(color, blurX, blurY, angle, distance, strength, inner, knockout, passes);
            }
            case BlurId:
            {
                var blurX = Fixed(reader);
                var blurY = Fixed(reader);
                var passes = (int)reader.UBits(5);
                reader.UBits(3);
                reader.Align();
                return new BlurFilter(blurX, blurY, passes);
            }
            case GlowId:
            {
                var color = SwfColor.ReadRgba(reader);
                var blurX = Fixed(reader);
                var blurY = Fixed(reader);
                var strength = reader.Fixed8();
                var inner = reader.Flag();
                var knockout = reader.Flag();
                reader.Flag();
                var passes = (int)reader.UBits(5);
                reader.Align();
                return new GlowFilter(color, blurX, blurY, strength, inner, knockout, passes);
            }
            case BevelId:
            {
                var shadow = SwfColor.ReadRgba(reader);
                var highlight = SwfColor.ReadRgba(reader);
                var blurX = Fixed(reader);
                var blurY = Fixed(reader);
                var angle = Fixed(reader);
                var distance = Fixed(reader);
                var strength = reader.Fixed8();
                var inner = reader.Flag();
                var knockout = reader.Flag();
                reader.Flag();
                var onTop = reader.Flag();
                var passes = (int)reader.UBits(4);
                reader.Align();
                return new BevelFilter(shadow, highlight, blurX, blurY, angle, distance, strength, inner, knockout, onTop, passes);
            }
            case GradientGlowId:
            case GradientBevelId:
            {
                var count = reader.U8();
                var colors = new SwfColor[count];
                for (var i = 0; i < count; i++) colors[i] = SwfColor.ReadRgba(reader);
                var stops = new List<GradientStop>(count);
                for (var i = 0; i < count; i++) stops.Add(new GradientStop(reader.U8(), colors[i]));
                var blurX = Fixed(reader);
                var blurY = Fixed(reader);
                var angle = Fixed(reader);
                var distance = Fixed(reader);
                var strength = reader.Fixed8();
                var inner = reader.Flag();
                var knockout = reader.Flag();
                reader.Flag();
                var onTop = reader.Flag();
                var passes = (int)reader.UBits(4);
                reader.Align();
                return new GradientGlowFilter(id == GradientBevelId, stops, blurX, blurY, angle, distance, strength, inner, knockout, onTop, passes);
            }
            case ConvolutionId:
            {
                var columns = reader.U8();
                var rows = reader.U8();
                var divisor = Float(reader);
                var bias = Float(reader);
                var matrix = new double[columns * rows];
                for (var i = 0; i < matrix.Length; i++) matrix[i] = Float(reader);
                var color = SwfColor.ReadRgba(reader);
                reader.UBits(6);
                var clamp = reader.Flag();
                var preserve = reader.Flag();
                reader.Align();
                return new ConvolutionFilter(columns, rows, divisor, bias, matrix, color, clamp, preserve);
            }
            case ColorMatrixId:
            {
                var matrix = new double[ColorMatrixSize];
                for (var i = 0; i < matrix.Length; i++) matrix[i] = Float(reader);
                return new ColorMatrixFilter(matrix);
            }
            default:
                throw new SwfFormatException($"unknown filter id {id}");
        }
    }

    private static double Fixed(SwfReader reader) => unchecked((int)reader.U32()) / 65536.0;

    private static double Float(SwfReader reader) => BitConverter.Int32BitsToSingle(unchecked((int)reader.U32()));
}
