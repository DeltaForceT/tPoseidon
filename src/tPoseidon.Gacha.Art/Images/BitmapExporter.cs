using System.IO.Compression;
using tPoseidon.Gacha.Art.Swf;

namespace tPoseidon.Gacha.Art.Images;

public sealed record ExportedBitmap(int Id, string Extension, byte[] Content, int Width, int Height, bool Alpha);

public static class BitmapExporter
{
    private const int ColorMapped = 3;
    private const int Rgb15 = 4;
    private const int Rgb32 = 5;

    public static ExportedBitmap Export(SwfLibrary library, BitmapDefinition bitmap)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(bitmap);
        var reader = new SwfReader(library.File.Data, bitmap.Offset, bitmap.Length);
        reader.U16();
        switch (bitmap.TagCode)
        {
            case SwfTagCode.DefineBits:
            {
                var tables = library.JpegTables is { } tag ? library.File.Data.AsSpan(tag.Offset, tag.Length).ToArray() : [];
                var jpeg = JpegSegments.Normalize(Concat(tables, reader.Bytes(reader.Remaining)));
                var (width, height) = JpegDecoder.ReadSize(jpeg);
                return new ExportedBitmap(bitmap.Id, "jpg", jpeg, width, height, false);
            }
            case SwfTagCode.DefineBitsJpeg2:
            {
                var jpeg = JpegSegments.Normalize(reader.Bytes(reader.Remaining));
                var (width, height) = JpegDecoder.ReadSize(jpeg);
                return new ExportedBitmap(bitmap.Id, "jpg", jpeg, width, height, false);
            }
            case SwfTagCode.DefineBitsJpeg3:
            {
                var jpegLength = (int)reader.U32();
                var jpeg = JpegSegments.Normalize(reader.Bytes(jpegLength));
                var alphaData = reader.Bytes(reader.Remaining);
                var image = JpegDecoder.Decode(jpeg);
                var alpha = Inflate(alphaData, image.Width * image.Height);
                var rgba = new byte[image.Width * image.Height * 4];
                for (var i = 0; i < image.Width * image.Height; i++)
                {
                    var a = alpha[i];
                    rgba[i * 4] = Unmultiply(image.Pixels[i * 3], a);
                    rgba[(i * 4) + 1] = Unmultiply(image.Pixels[(i * 3) + 1], a);
                    rgba[(i * 4) + 2] = Unmultiply(image.Pixels[(i * 3) + 2], a);
                    rgba[(i * 4) + 3] = a;
                }
                return new ExportedBitmap(bitmap.Id, "png", PngWriter.Encode(image.Width, image.Height, rgba, true), image.Width, image.Height, true);
            }
            case SwfTagCode.DefineBitsLossless:
            case SwfTagCode.DefineBitsLossless2:
                return ExportLossless(bitmap, reader, bitmap.TagCode == SwfTagCode.DefineBitsLossless2);
            default:
                throw new SwfFormatException($"tag {bitmap.TagCode} is not a bitmap");
        }
    }

    private static ExportedBitmap ExportLossless(BitmapDefinition bitmap, SwfReader reader, bool withAlpha)
    {
        var format = reader.U8();
        var width = reader.U16();
        var height = reader.U16();
        var tableSize = format == ColorMapped ? reader.U8() + 1 : 0;
        var compressed = reader.Bytes(reader.Remaining);
        var pixels = new byte[width * height * 4];
        switch (format)
        {
            case ColorMapped:
            {
                var entry = withAlpha ? 4 : 3;
                var stride = (width + 3) & ~3;
                var data = Inflate(compressed, (tableSize * entry) + (stride * height));
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var index = data[(tableSize * entry) + (y * stride) + x];
                        var target = ((y * width) + x) * 4;
                        if (index >= tableSize) continue;
                        var source = index * entry;
                        var a = withAlpha ? data[source + 3] : (byte)255;
                        pixels[target] = withAlpha ? Unmultiply(data[source], a) : data[source];
                        pixels[target + 1] = withAlpha ? Unmultiply(data[source + 1], a) : data[source + 1];
                        pixels[target + 2] = withAlpha ? Unmultiply(data[source + 2], a) : data[source + 2];
                        pixels[target + 3] = a;
                    }
                }
                break;
            }
            case Rgb15 when !withAlpha:
            {
                var stride = ((width * 2) + 3) & ~3;
                var data = Inflate(compressed, stride * height);
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var at = (y * stride) + (x * 2);
                        var value = (data[at] << 8) | data[at + 1];
                        var target = ((y * width) + x) * 4;
                        pixels[target] = Expand5((value >> 10) & 0x1F);
                        pixels[target + 1] = Expand5((value >> 5) & 0x1F);
                        pixels[target + 2] = Expand5(value & 0x1F);
                        pixels[target + 3] = 255;
                    }
                }
                break;
            }
            case Rgb32:
            {
                var data = Inflate(compressed, width * height * 4);
                for (var i = 0; i < width * height; i++)
                {
                    var a = withAlpha ? data[i * 4] : (byte)255;
                    pixels[i * 4] = withAlpha ? Unmultiply(data[(i * 4) + 1], a) : data[(i * 4) + 1];
                    pixels[(i * 4) + 1] = withAlpha ? Unmultiply(data[(i * 4) + 2], a) : data[(i * 4) + 2];
                    pixels[(i * 4) + 2] = withAlpha ? Unmultiply(data[(i * 4) + 3], a) : data[(i * 4) + 3];
                    pixels[(i * 4) + 3] = a;
                }
                break;
            }
            default:
                throw new SwfFormatException($"bitmap {bitmap.Id}: lossless format {format} is not defined for this tag");
        }
        var hasAlpha = false;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 255)
            {
                hasAlpha = true;
                break;
            }
        }
        var content = hasAlpha ? PngWriter.Encode(width, height, pixels, true) : PngWriter.Encode(width, height, DropAlpha(pixels), false);
        return new ExportedBitmap(bitmap.Id, "png", content, width, height, hasAlpha);
    }

    private static byte[] DropAlpha(byte[] rgba)
    {
        var rgb = new byte[rgba.Length / 4 * 3];
        for (int i = 0, j = 0; i < rgba.Length; i += 4, j += 3)
        {
            rgb[j] = rgba[i];
            rgb[j + 1] = rgba[i + 1];
            rgb[j + 2] = rgba[i + 2];
        }
        return rgb;
    }

    private static byte Unmultiply(byte value, byte alpha)
    {
        if (alpha == 0) return 0;
        if (alpha == 255) return value;
        var result = ((value * 255) + (alpha / 2)) / alpha;
        return (byte)Math.Min(255, result);
    }

    private static byte Expand5(int value) => (byte)((value << 3) | (value >> 2));

    private static byte[] Inflate(byte[] compressed, int expected)
    {
        using var input = new MemoryStream(compressed, writable: false);
        using var zlib = new ZLibStream(input, CompressionMode.Decompress);
        var output = new byte[expected];
        var filled = 0;
        while (filled < expected)
        {
            var read = zlib.Read(output, filled, expected - filled);
            if (read == 0) throw new SwfFormatException($"bitmap data inflates to {filled} byte(s), {expected} expected");
            filled += read;
        }
        return output;
    }

    private static byte[] Concat(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        first.CopyTo(result, 0);
        second.CopyTo(result, first.Length);
        return result;
    }
}
