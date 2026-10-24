using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace tPoseidon.Tools.GachaArt.Tests;

internal sealed class BitWriter
{
    private readonly List<byte> _bytes = new();
    private int _current;
    private int _used;

    public BitWriter Bits(long value, int count)
    {
        for (var i = count - 1; i >= 0; i--)
        {
            _current = (_current << 1) | (int)((value >> i) & 1);
            _used++;
            if (_used == 8)
            {
                _bytes.Add((byte)_current);
                _current = 0;
                _used = 0;
            }
        }
        return this;
    }

    public BitWriter Align()
    {
        if (_used > 0) Bits(0, 8 - _used);
        return this;
    }

    public BitWriter U8(int value)
    {
        Align();
        _bytes.Add((byte)value);
        return this;
    }

    public BitWriter U16(int value)
    {
        Align();
        _bytes.Add((byte)value);
        _bytes.Add((byte)(value >> 8));
        return this;
    }

    public BitWriter U32(uint value)
    {
        Align();
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        foreach (var b in buffer) _bytes.Add(b);
        return this;
    }

    public BitWriter Raw(byte[] data)
    {
        Align();
        _bytes.AddRange(data);
        return this;
    }

    public BitWriter Text(string value)
    {
        Align();
        _bytes.AddRange(Encoding.UTF8.GetBytes(value));
        _bytes.Add(0);
        return this;
    }

    public BitWriter Rect(int xMin, int xMax, int yMin, int yMax)
    {
        Align();
        const int bits = 16;
        Bits(bits, 5).Bits(xMin, bits).Bits(xMax, bits).Bits(yMin, bits).Bits(yMax, bits);
        return Align();
    }

    public BitWriter Matrix(int translateX, int translateY, double scaleX = 1, double scaleY = 1)
    {
        Align();
        var scaled = scaleX != 1 || scaleY != 1;
        Bits(scaled ? 1 : 0, 1);
        if (scaled)
        {
            Bits(24, 5).Bits((long)(scaleX * 65536), 24).Bits((long)(scaleY * 65536), 24);
        }
        Bits(0, 1);
        Bits(16, 5).Bits(translateX, 16).Bits(translateY, 16);
        return Align();
    }

    public BitWriter AlphaTransform(int alphaMultiplier256)
    {
        Align();
        Bits(0, 1).Bits(1, 1).Bits(10, 4);
        Bits(256, 10).Bits(256, 10).Bits(256, 10).Bits(alphaMultiplier256, 10);
        return Align();
    }

    public byte[] ToArray()
    {
        Align();
        return _bytes.ToArray();
    }
}

internal sealed class SwfBuilder
{
    private readonly List<byte> _tags = new();

    public int Frames { get; set; } = 1;

    public static byte[] Tag(int code, byte[] body)
    {
        var output = new List<byte>();
        if (body.Length < 0x3F)
        {
            var header = (code << 6) | body.Length;
            output.Add((byte)header);
            output.Add((byte)(header >> 8));
        }
        else
        {
            var header = (code << 6) | 0x3F;
            output.Add((byte)header);
            output.Add((byte)(header >> 8));
            output.Add((byte)body.Length);
            output.Add((byte)(body.Length >> 8));
            output.Add((byte)(body.Length >> 16));
            output.Add((byte)(body.Length >> 24));
        }
        output.AddRange(body);
        return output.ToArray();
    }

    public SwfBuilder Add(int code, byte[] body)
    {
        _tags.AddRange(Tag(code, body));
        return this;
    }

    public SwfBuilder Add(int code, BitWriter body) => Add(code, body.ToArray());

    public byte[] Build(bool compressed)
    {
        var header = new BitWriter().Rect(0, 640 * 20, 0, 480 * 20).U16(20 << 8).U16(Frames).ToArray();
        var body = header.Concat(_tags).Concat(Tag(0, [])).ToArray();
        var length = 8 + body.Length;
        var file = new List<byte> { (byte)(compressed ? 'C' : 'F'), (byte)'W', (byte)'S', 8 };
        file.Add((byte)length);
        file.Add((byte)(length >> 8));
        file.Add((byte)(length >> 16));
        file.Add((byte)(length >> 24));
        if (compressed)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(body);
            file.AddRange(output.ToArray());
        }
        else
        {
            file.AddRange(body);
        }
        return file.ToArray();
    }

    public static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true)) zlib.Write(data);
        return output.ToArray();
    }

    public static byte[] SolidRectangleShape(int id, int width, int height, byte red, byte green, byte blue)
    {
        var w = new BitWriter().U16(id).Rect(0, width, 0, height);
        w.U8(1).U8(0x00).U8(red).U8(green).U8(blue);
        w.U8(0);
        w.Bits(1, 4).Bits(0, 4);
        w.Bits(0, 1).Bits(0b00101, 5).Bits(16, 5).Bits(0, 16).Bits(0, 16).Bits(1, 1);
        StraightX(w, width);
        StraightY(w, height);
        StraightX(w, -width);
        StraightY(w, -height);
        w.Bits(0, 1).Bits(0, 5);
        return w.ToArray();
    }

    public static void StraightX(BitWriter w, int dx)
    {
        w.Bits(1, 1).Bits(1, 1).Bits(14, 4).Bits(0, 1).Bits(0, 1).Bits(dx, 16);
    }

    public static void StraightY(BitWriter w, int dy)
    {
        w.Bits(1, 1).Bits(1, 1).Bits(14, 4).Bits(0, 1).Bits(1, 1).Bits(dy, 16);
    }

    public static byte[] GrayJpeg()
    {
        var jpeg = new List<byte> { 0xFF, 0xD8 };
        jpeg.AddRange([0xFF, 0xDB, 0x00, 0x43, 0x00]);
        jpeg.AddRange(Enumerable.Repeat((byte)1, 64));
        jpeg.AddRange([0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x08, 0x00, 0x08, 0x01, 0x01, 0x11, 0x00]);
        jpeg.AddRange([0xFF, 0xC4, 0x00, 0x14, 0x00, 0x01]);
        jpeg.AddRange(new byte[15]);
        jpeg.Add(0x00);
        jpeg.AddRange([0xFF, 0xC4, 0x00, 0x14, 0x10, 0x01]);
        jpeg.AddRange(new byte[15]);
        jpeg.Add(0x00);
        jpeg.AddRange([0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01, 0x00, 0x00, 0x3F, 0x00]);
        jpeg.Add(0x3F);
        jpeg.AddRange([0xFF, 0xD9]);
        return jpeg.ToArray();
    }
}

internal static class PngReader
{
    public static (int Width, int Height, int Channels, byte[] Pixels) Read(byte[] png)
    {
        var position = 8;
        int width = 0, height = 0, channels = 0;
        using var idat = new MemoryStream();
        while (position < png.Length)
        {
            var length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(position));
            var type = Encoding.ASCII.GetString(png, position + 4, 4);
            var data = png.AsSpan(position + 8, length);
            if (type == "IHDR")
            {
                width = BinaryPrimitives.ReadInt32BigEndian(data);
                height = BinaryPrimitives.ReadInt32BigEndian(data[4..]);
                channels = data[9] == 6 ? 4 : 3;
            }
            else if (type == "IDAT")
            {
                idat.Write(data);
            }
            position += 12 + length;
        }
        idat.Position = 0;
        using var zlib = new ZLibStream(idat, CompressionMode.Decompress);
        using var raw = new MemoryStream();
        zlib.CopyTo(raw);
        var filtered = raw.ToArray();
        var stride = width * channels;
        var pixels = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var filter = filtered[y * (stride + 1)];
            for (var i = 0; i < stride; i++)
            {
                var value = filtered[(y * (stride + 1)) + 1 + i];
                int left = i >= channels ? pixels[(y * stride) + i - channels] : 0;
                int up = y > 0 ? pixels[((y - 1) * stride) + i] : 0;
                int upLeft = y > 0 && i >= channels ? pixels[((y - 1) * stride) + i - channels] : 0;
                var predicted = filter switch
                {
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                };
                pixels[(y * stride) + i] = (byte)(value + predicted);
            }
        }
        return (width, height, channels, pixels);
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
}
