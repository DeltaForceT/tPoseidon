using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace tPoseidon.Gacha.Art.Images;

public static class PngWriter
{
    private const byte ColorTypeRgb = 2;
    private const byte ColorTypeRgba = 6;
    private const byte BitDepth = 8;

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Encode(int width, int height, byte[] pixels, bool alpha)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        var channels = alpha ? 4 : 3;
        if (width <= 0 || height <= 0 || pixels.Length != width * height * channels) throw new ArgumentException("pixel buffer does not match the image size", nameof(pixels));
        using var output = new MemoryStream();
        output.Write(Signature);
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header, width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = BitDepth;
        header[9] = alpha ? ColorTypeRgba : ColorTypeRgb;
        WriteChunk(output, "IHDR", header);
        WriteChunk(output, "IDAT", Compress(Filter(width, height, pixels, channels)));
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    public static uint Crc32(ReadOnlySpan<byte> data, uint seed = 0xFFFFFFFF)
    {
        var crc = seed;
        foreach (var value in data) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
        return crc;
    }

    private static byte[] Filter(int width, int height, byte[] pixels, int channels)
    {
        var stride = width * channels;
        var output = new byte[(stride + 1) * height];
        var candidate = new byte[stride];
        var best = new byte[stride];
        for (var y = 0; y < height; y++)
        {
            var row = pixels.AsSpan(y * stride, stride);
            var previous = y == 0 ? Span<byte>.Empty : pixels.AsSpan((y - 1) * stride, stride);
            var bestScore = long.MaxValue;
            byte bestType = 0;
            for (byte type = 0; type <= 4; type++)
            {
                long score = 0;
                for (var i = 0; i < stride; i++)
                {
                    int left = i >= channels ? row[i - channels] : 0;
                    int up = previous.IsEmpty ? 0 : previous[i];
                    int upLeft = !previous.IsEmpty && i >= channels ? previous[i - channels] : 0;
                    var predicted = type switch
                    {
                        1 => left,
                        2 => up,
                        3 => (left + up) / 2,
                        4 => Paeth(left, up, upLeft),
                        _ => 0,
                    };
                    var value = (byte)(row[i] - predicted);
                    candidate[i] = value;
                    score += value < 128 ? value : 256 - value;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    bestType = type;
                    candidate.CopyTo(best, 0);
                }
            }
            output[y * (stride + 1)] = bestType;
            best.CopyTo(output, (y * (stride + 1)) + 1);
        }
        return output;
    }

    private static int Paeth(int a, int b, int c)
    {
        var p = a + b - c;
        var pa = Math.Abs(p - a);
        var pb = Math.Abs(p - b);
        var pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc) return a;
        return pb <= pc ? b : c;
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(data);
        }
        return output.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        output.Write(length);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);
        var crc = Crc32(data, Crc32(typeBytes)) ^ 0xFFFFFFFF;
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        output.Write(crcBytes);
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
