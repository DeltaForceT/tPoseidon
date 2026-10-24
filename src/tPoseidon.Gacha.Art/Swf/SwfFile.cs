using System.Buffers.Binary;
using System.IO.Compression;

namespace tPoseidon.Gacha.Art.Swf;

public static class SwfTagCode
{
    public const int End = 0;
    public const int ShowFrame = 1;
    public const int DefineShape = 2;
    public const int PlaceObject = 4;
    public const int RemoveObject = 5;
    public const int DefineBits = 6;
    public const int JpegTables = 8;
    public const int SetBackgroundColor = 9;
    public const int DefineFont = 10;
    public const int DefineText = 11;
    public const int DoAction = 12;
    public const int DefineFontInfo = 13;
    public const int DefineSound = 14;
    public const int StartSound = 15;
    public const int DefineButtonSound = 17;
    public const int DefineBitsLossless = 20;
    public const int DefineBitsJpeg2 = 21;
    public const int DefineShape2 = 22;
    public const int PlaceObject2 = 26;
    public const int RemoveObject2 = 28;
    public const int DefineShape3 = 32;
    public const int DefineText2 = 33;
    public const int DefineButton2 = 34;
    public const int DefineBitsJpeg3 = 35;
    public const int DefineBitsLossless2 = 36;
    public const int DefineEditText = 37;
    public const int DefineSprite = 39;
    public const int FrameLabel = 43;
    public const int DefineMorphShape = 46;
    public const int DefineFont2 = 48;
    public const int ExportAssets = 56;
    public const int DefineFontInfo2 = 62;
    public const int PlaceObject3 = 70;
    public const int DefineFont3 = 75;
    public const int DefineShape4 = 83;
}

public sealed record SwfTag(int Code, int Offset, int Length);

public sealed class SwfFile
{
    private const int HeaderSize = 8;

    private SwfFile(byte[] data, int version, SwfRect frameSize, double frameRate, int frameCount, int tagsOffset)
    {
        Data = data;
        Version = version;
        FrameSize = frameSize;
        FrameRate = frameRate;
        FrameCount = frameCount;
        TagsOffset = tagsOffset;
    }

    public byte[] Data { get; }

    public int Version { get; }

    public SwfRect FrameSize { get; }

    public double FrameRate { get; }

    public int FrameCount { get; }

    public int TagsOffset { get; }

    public static SwfFile Parse(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length < HeaderSize) throw new SwfFormatException("file shorter than the SWF header");
        var signature = (char)file[0];
        if (file[1] != 'W' || file[2] != 'S' || (signature != 'F' && signature != 'C')) throw new SwfFormatException("not an SWF file");
        var version = file[3];
        var declaredLength = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(4));
        byte[] data;
        if (signature == 'C')
        {
            data = new byte[declaredLength];
            Array.Copy(file, data, HeaderSize);
            using var input = new MemoryStream(file, HeaderSize, file.Length - HeaderSize, writable: false);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            var filled = HeaderSize;
            while (filled < declaredLength)
            {
                var read = zlib.Read(data, filled, declaredLength - filled);
                if (read == 0) break;
                filled += read;
            }
            if (filled != declaredLength) Array.Resize(ref data, filled);
        }
        else
        {
            data = file;
        }
        var reader = new SwfReader(data, HeaderSize, data.Length - HeaderSize);
        var frameSize = SwfRect.Read(reader);
        var rate = reader.U16() / 256.0;
        var frames = reader.U16();
        return new SwfFile(data, version, frameSize, rate, frames, reader.Position);
    }

    public IReadOnlyList<SwfTag> RootTags() => ReadTags(Data, TagsOffset, Data.Length);

    public static IReadOnlyList<SwfTag> ReadTags(byte[] data, int start, int end)
    {
        ArgumentNullException.ThrowIfNull(data);
        var tags = new List<SwfTag>();
        var reader = new SwfReader(data, start, end - start);
        while (reader.Remaining >= 2)
        {
            var header = reader.U16();
            var code = header >> 6;
            var length = header & 0x3F;
            if (length == 0x3F) length = checked((int)reader.U32());
            if (length > reader.Remaining) throw new SwfFormatException($"tag {code} at {reader.Position} claims {length} byte(s), {reader.Remaining} left");
            tags.Add(new SwfTag(code, reader.Position, length));
            reader.Skip(length);
            if (code == SwfTagCode.End) break;
        }
        return tags;
    }

    public SwfReader Open(SwfTag tag)
    {
        ArgumentNullException.ThrowIfNull(tag);
        return new SwfReader(Data, tag.Offset, tag.Length);
    }
}
