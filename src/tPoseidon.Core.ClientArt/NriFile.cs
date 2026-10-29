using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Core.ClientArt;

public sealed class NriFile
{
    public const int NameCodePage = 949;
    public const string StandingAnimation = "서있기";
    public const string FrontDirection = "아래";

    private const int NoriSignature = 0x49524F4E;
    private const int GawiSignature = 0x49574147;
    private const int PaletteSignature = 0x5F4C4150;
    private const int NoriHeaderSize = 40;
    private const int NoriAnimationCountOffset = 0x1C;
    private const int GawiHeaderSize = 44;
    private const int GawiBitsOffset = 8;
    private const int GawiCompressedOffset = 12;
    private const int GawiFlagsOffset = 16;
    private const int GawiImageCountOffset = 36;
    private const int PaletteFlag = 1;
    private const int PaletteHeaderSize = 32;
    private const int PaletteColors = 256;
    private const int PaletteColorBytes = PaletteColors * 3;
    private const int PalettePairCountOffset = 0x18;
    private const int PalettePairSize = 8;
    private const int SubImageHeaderSize = 24;
    private const int AnimationNameSize = 32;
    private const int AnimationFrameHeaderSize = 8;
    private const int AnimationLayerSize = 28;
    private const int LayerImageOffset = 0;
    private const int LayerXOffset = 4;
    private const int LayerYOffset = 8;
    private const int LayerFlagsOffset = 16;
    private const int LayerAlphaOffset = 24;
    private const int FlipHorizontal = 1;
    private const int FlipVertical = 2;
    private const int FullAlphaSteps = 32;
    private const int MaxSide = 4096;
    private const int OffsetSize = 4;
    private const int MaxLayersPerFrame = 256;
    private const ushort TransparentRgb555 = 0x7C1F;
    private const int TransparentKey24 = 0xFF00FF;

    private readonly byte[] _data;
    private readonly int _bits;
    private readonly bool _compressed;
    private readonly int _imageBase;
    private readonly int _animationCount;
    private readonly byte[]? _palette;
    private (int[] Starts, int End)? _layout;

    private NriFile(byte[] data)
    {
        _data = data;
        if (data.Length < NoriHeaderSize + GawiHeaderSize
            || Int(0) != NoriSignature
            || Int(NoriHeaderSize) != GawiSignature)
        {
            throw new InvalidDataException("Not a NORI sprite file.");
        }

        _animationCount = Int(NoriAnimationCountOffset);
        _bits = Int(NoriHeaderSize + GawiBitsOffset);
        _compressed = Int(NoriHeaderSize + GawiCompressedOffset) == 1;
        var flags = Int(NoriHeaderSize + GawiFlagsOffset);
        ImageCount = Int(NoriHeaderSize + GawiImageCountOffset);
        if (_bits is not (8 or 16 or 24) || ImageCount < 0 || ImageCount > data.Length / OffsetSize)
        {
            throw new InvalidDataException("The NORI header holds values this reader does not know.");
        }

        var imageTable = NoriHeaderSize + GawiHeaderSize;
        if ((flags & PaletteFlag) != 0)
        {
            if (Int(imageTable) != PaletteSignature)
            {
                throw new InvalidDataException("NORI palette header missing.");
            }

            var paletteStart = imageTable + PaletteHeaderSize;
            Require(paletteStart, PaletteColorBytes);
            _palette = data.AsSpan(paletteStart, PaletteColorBytes).ToArray();
            imageTable = paletteStart + PaletteColorBytes + Int(imageTable + PalettePairCountOffset) * PalettePairSize;
        }

        _imageBase = imageTable + ImageCount * OffsetSize;
        Require(imageTable, ImageCount * OffsetSize);
    }

    public int ImageCount { get; }

    public static NriFile Parse(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return new NriFile(data);
    }

    public ClientPicture ReadImage(int index)
    {
        if (index < 0 || index >= ImageCount)
        {
            throw new InvalidDataException($"The sprite has {ImageCount} images and image {index} was asked for.");
        }

        var cursor = Layout().Starts[index];
        if (Int(cursor) < 1)
        {
            throw new InvalidDataException("The image group is empty.");
        }

        cursor += OffsetSize;
        Require(cursor, SubImageHeaderSize);
        var size = Int(cursor);
        var width = Int(cursor + 4);
        var height = Int(cursor + 8);
        if (width is < 1 or > MaxSide || height is < 1 or > MaxSide)
        {
            throw new InvalidDataException($"The image size {width} x {height} is not usable.");
        }

        cursor += SubImageHeaderSize;
        Require(cursor, size);
        return Decode(_data.AsSpan(cursor, size), width, height);
    }

    public ClientPicture ReadStanding()
    {
        var animations = ReadAnimationStarts();
        if (animations.Count == 0)
        {
            throw new InvalidDataException("The sprite has no animation.");
        }

        var front = $"{StandingAnimation}_{FrontDirection}";
        var chosen = animations.FirstOrDefault(animation => animation.Name == front);
        if (chosen.Name is null)
        {
            chosen = animations.FirstOrDefault(animation => animation.Name.StartsWith(StandingAnimation, StringComparison.Ordinal));
        }

        return ComposeFirstFrame((chosen.Name is null ? animations[0] : chosen).Start);
    }

    private ClientPicture ComposeFirstFrame(int animationStart)
    {
        var frameCount = Int(animationStart + AnimationNameSize);
        if (frameCount < 1)
        {
            throw new InvalidDataException("The animation has no frame.");
        }

        var frameTable = animationStart + AnimationNameSize + OffsetSize;
        var frame = frameTable + frameCount * OffsetSize + Int(frameTable);
        var layerCount = Int(frame + OffsetSize);
        if (layerCount is < 1 or > MaxLayersPerFrame)
        {
            throw new InvalidDataException($"The animation frame holds {layerCount} layers.");
        }

        var layers = new List<(ClientPicture Picture, int X, int Y, int Flags, int Alpha)>(layerCount);
        var at = frame + AnimationFrameHeaderSize;
        for (var i = 0; i < layerCount; i++)
        {
            Require(at, AnimationLayerSize);
            var alpha = Int(at + LayerAlphaOffset);
            var picture = ReadImage(Int(at + LayerImageOffset));
            layers.Add((picture, Int(at + LayerXOffset), Int(at + LayerYOffset), Int(at + LayerFlagsOffset), alpha is <= 0 or > FullAlphaSteps ? FullAlphaSteps : alpha));
            at += AnimationLayerSize;
        }

        var left = layers.Min(layer => layer.X);
        var top = layers.Min(layer => layer.Y);
        var width = layers.Max(layer => layer.X + layer.Picture.Width) - left;
        var height = layers.Max(layer => layer.Y + layer.Picture.Height) - top;
        if (width > MaxSide || height > MaxSide)
        {
            throw new InvalidDataException($"The composed frame size {width} x {height} is not usable.");
        }

        var canvas = new byte[width * height * 4];
        foreach (var (picture, x, y, flags, alpha) in layers)
        {
            Blend(canvas, width, picture, x - left, y - top, (flags & FlipHorizontal) != 0, (flags & FlipVertical) != 0, Math.Min(255, alpha * 255 >> 5));
        }

        return Trim(new ClientPicture(width, height, canvas));
    }

    private static void Blend(byte[] canvas, int canvasWidth, ClientPicture picture, int originX, int originY, bool flipX, bool flipY, int opacity)
    {
        for (var row = 0; row < picture.Height; row++)
        {
            var sourceRow = flipY ? picture.Height - 1 - row : row;
            for (var column = 0; column < picture.Width; column++)
            {
                var sourceColumn = flipX ? picture.Width - 1 - column : column;
                var source = (sourceRow * picture.Width + sourceColumn) * 4;
                var alpha = picture.Rgba[source + 3] * opacity / 255;
                if (alpha == 0) continue;
                var target = ((originY + row) * canvasWidth + originX + column) * 4;
                var below = canvas[target + 3];
                var outAlpha = alpha + below * (255 - alpha) / 255;
                for (var channel = 0; channel < 3; channel++)
                {
                    canvas[target + channel] = (byte)((picture.Rgba[source + channel] * alpha + canvas[target + channel] * below * (255 - alpha) / 255) / outAlpha);
                }

                canvas[target + 3] = (byte)outAlpha;
            }
        }
    }

    private static ClientPicture Trim(ClientPicture picture)
    {
        int minX = picture.Width, minY = picture.Height, maxX = -1, maxY = -1;
        for (var y = 0; y < picture.Height; y++)
        {
            for (var x = 0; x < picture.Width; x++)
            {
                if (picture.Rgba[(y * picture.Width + x) * 4 + 3] == 0) continue;
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minY = Math.Min(minY, y);
                maxY = Math.Max(maxY, y);
            }
        }

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        if (maxX < 0 || (width == picture.Width && height == picture.Height))
        {
            return picture;
        }

        var trimmed = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            Buffer.BlockCopy(picture.Rgba, ((minY + y) * picture.Width + minX) * 4, trimmed, y * width * 4, width * 4);
        }

        return new ClientPicture(width, height, trimmed);
    }

    private List<(string Name, int Start)> ReadAnimationStarts()
    {
        var animationTable = Layout().End;
        if (_animationCount < 0 || _animationCount > _data.Length / OffsetSize)
        {
            throw new InvalidDataException("The animation count is not usable.");
        }

        var animationBase = animationTable + _animationCount * OffsetSize;
        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(NameCodePage)
            ?? throw new InvalidOperationException("The Korean text encoding is not available.");
        var starts = new List<(string, int)>(_animationCount);
        for (var i = 0; i < _animationCount; i++)
        {
            var start = animationBase + Int(animationTable + i * OffsetSize);
            Require(start, AnimationNameSize + OffsetSize);
            var raw = _data.AsSpan(start, AnimationNameSize);
            var end = raw.IndexOf((byte)0);
            starts.Add((encoding.GetString(end < 0 ? raw : raw[..end]), start));
        }

        return starts;
    }

    private (int[] Starts, int End) Layout()
    {
        if (_layout is { } known)
        {
            return known;
        }

        var starts = new int[ImageCount];
        var cursor = _imageBase;
        for (var i = 0; i < ImageCount; i++)
        {
            starts[i] = cursor;
            var subImages = Int(cursor);
            cursor += OffsetSize;
            for (var s = 0; s < subImages; s++)
            {
                var size = Int(cursor);
                if (size < 0)
                {
                    throw new InvalidDataException("An image has a negative size.");
                }

                cursor += SubImageHeaderSize + size;
            }
        }

        _layout = (starts, cursor);
        return _layout.Value;
    }

    private ClientPicture Decode(ReadOnlySpan<byte> pixels, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        var bytesPerPixel = _bits / 8;
        if (_compressed && _bits != 8)
        {
            DecodeRows(pixels, width, height, bytesPerPixel, rgba);
        }
        else
        {
            if (pixels.Length != width * height * bytesPerPixel)
            {
                throw new InvalidDataException($"The image data is {pixels.Length} bytes; {width * height * bytesPerPixel} were expected.");
            }

            for (var i = 0; i < width * height; i++)
            {
                WritePixel(pixels.Slice(i * bytesPerPixel, bytesPerPixel), rgba.AsSpan(i * 4, 4));
            }
        }

        return new ClientPicture(width, height, rgba);
    }

    private void DecodeRows(ReadOnlySpan<byte> pixels, int width, int height, int bytesPerPixel, byte[] rgba)
    {
        var at = 0;
        for (var row = 0; row < height; row++)
        {
            if (at + 2 > pixels.Length)
            {
                throw new InvalidDataException("The compressed image ends inside a row.");
            }

            var rowBytes = BinaryPrimitives.ReadUInt16LittleEndian(pixels[at..]);
            var rowEnd = at + rowBytes;
            if (rowBytes < 2 || rowEnd > pixels.Length)
            {
                throw new InvalidDataException("A compressed row has a bad length.");
            }

            var cursor = at + 2;
            var x = 0;
            while (x < width)
            {
                if (cursor + 4 > rowEnd)
                {
                    throw new InvalidDataException("A compressed row ends before its width is filled.");
                }

                var skip = BinaryPrimitives.ReadUInt16LittleEndian(pixels[cursor..]);
                var count = BinaryPrimitives.ReadUInt16LittleEndian(pixels[(cursor + 2)..]);
                cursor += 4;
                x += skip;
                if (x + count > width || cursor + count * bytesPerPixel > rowEnd)
                {
                    throw new InvalidDataException("A compressed run leaves the row.");
                }

                for (var i = 0; i < count; i++)
                {
                    WritePixel(pixels.Slice(cursor, bytesPerPixel), rgba.AsSpan((row * width + x) * 4, 4));
                    cursor += bytesPerPixel;
                    x++;
                }
            }

            at = rowEnd;
        }

        if (at != pixels.Length)
        {
            throw new InvalidDataException("The compressed image has bytes left after its last row.");
        }
    }

    private void WritePixel(ReadOnlySpan<byte> source, Span<byte> target)
    {
        switch (_bits)
        {
            case 8:
                if (source[0] == 0) return;
                if (_palette is null)
                {
                    throw new InvalidDataException("An 8-bit image needs a palette and the sprite has none.");
                }

                target[0] = _palette[source[0] * 3];
                target[1] = _palette[source[0] * 3 + 1];
                target[2] = _palette[source[0] * 3 + 2];
                break;
            case 16:
                var value = BinaryPrimitives.ReadUInt16LittleEndian(source);
                if (value == TransparentRgb555) return;
                target[0] = Expand5((value >> 10) & 0x1F);
                target[1] = Expand5((value >> 5) & 0x1F);
                target[2] = Expand5(value & 0x1F);
                break;
            default:
                if (((source[2] << 16) | (source[1] << 8) | source[0]) == TransparentKey24) return;
                target[0] = source[2];
                target[1] = source[1];
                target[2] = source[0];
                break;
        }

        target[3] = byte.MaxValue;
    }

    private static byte Expand5(int channel) => (byte)((channel << 3) | (channel >> 2));

    private int Int(int offset)
    {
        Require(offset, OffsetSize);
        return BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(offset, OffsetSize));
    }

    private void Require(int offset, int length)
    {
        if (offset < 0 || length < 0 || (long)offset + length > _data.Length)
        {
            throw new InvalidDataException("The sprite file ends before its data does.");
        }
    }
}
