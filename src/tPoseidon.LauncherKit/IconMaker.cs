using System.Buffers.Binary;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public static class IconMaker
{
    public const int MaxSide = 256;

    private const int HeaderBytes = 6;
    private const int EntryBytes = 16;
    private const int PngSignatureBytes = 8;
    private const int PngDimensionsOffset = 16;
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static byte[] FromPngs(IReadOnlyList<byte[]> pictures)
    {
        ArgumentNullException.ThrowIfNull(pictures);
        if (pictures.Count == 0)
        {
            throw new HostingException("An icon needs at least one picture.");
        }

        var sizes = pictures.Select(Side).ToList();
        if (sizes.Distinct().Count() != sizes.Count)
        {
            throw new HostingException("An icon cannot hold two pictures of the same size.");
        }

        var output = new byte[HeaderBytes + (EntryBytes * pictures.Count) + pictures.Sum(picture => picture.Length)];
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(4), (ushort)pictures.Count);
        var offset = HeaderBytes + (EntryBytes * pictures.Count);
        for (var i = 0; i < pictures.Count; i++)
        {
            var entry = output.AsSpan(HeaderBytes + (EntryBytes * i), EntryBytes);
            entry[0] = (byte)(sizes[i] == MaxSide ? 0 : sizes[i]);
            entry[1] = entry[0];
            BinaryPrimitives.WriteUInt16LittleEndian(entry[4..], 1);
            BinaryPrimitives.WriteUInt16LittleEndian(entry[6..], 32);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[8..], (uint)pictures[i].Length);
            BinaryPrimitives.WriteUInt32LittleEndian(entry[12..], (uint)offset);
            pictures[i].CopyTo(output, offset);
            offset += pictures[i].Length;
        }

        return output;
    }

    public static (int Width, int Height) Dimensions(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);
        if (png.Length < PngDimensionsOffset + 8 || !png.AsSpan(0, PngSignatureBytes).SequenceEqual(PngSignature))
        {
            throw new HostingException("This is not a PNG picture.", "Choose a PNG file.", null);
        }

        return (
            (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(PngDimensionsOffset)),
            (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(PngDimensionsOffset + 4)));
    }

    private static int Side(byte[] png)
    {
        var (width, height) = Dimensions(png);
        if (width != height || width is < 16 or > MaxSide)
        {
            throw new HostingException($"An icon picture must be square and between 16 and {MaxSide} pixels, this one is {width} x {height}.");
        }

        return width;
    }
}
