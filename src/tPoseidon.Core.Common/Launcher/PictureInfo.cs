using System.Buffers.Binary;

namespace tPoseidon.Core.Common.Launcher;

public enum PictureFormat
{
    Png,
    Jpeg,
}

public sealed record PictureInfo(PictureFormat Format, int Width, int Height)
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public string Extension => Format == PictureFormat.Png ? ".png" : ".jpg";

    public static PictureInfo Inspect(ReadOnlySpan<byte> bytes, long maxBytes)
    {
        if (bytes.Length > maxBytes)
        {
            throw new InvalidDataException($"The picture is {bytes.Length / 1024} KB, and the limit is {maxBytes / 1024} KB.");
        }

        return Inspect(bytes);
    }

    public static PictureInfo Inspect(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith(PngSignature))
        {
            return InspectPng(bytes);
        }

        if (bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            return InspectJpeg(bytes);
        }

        throw new InvalidDataException("The picture must be a PNG or a JPG file.");
    }

    private static PictureInfo InspectPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24 || !bytes.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            throw new InvalidDataException("The PNG file is damaged.");
        }

        return Sized(PictureFormat.Png, BinaryPrimitives.ReadInt32BigEndian(bytes[16..]), BinaryPrimitives.ReadInt32BigEndian(bytes[20..]));
    }

    private static PictureInfo InspectJpeg(ReadOnlySpan<byte> bytes)
    {
        var position = 2;
        while (position + 4 <= bytes.Length && bytes[position] == 0xFF)
        {
            var marker = bytes[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }

            if (marker is 0x01 or (>= 0xD0 and <= 0xD8))
            {
                position += 2;
                continue;
            }

            if (marker is >= 0xC0 and <= 0xCF and not (0xC4 or 0xC8 or 0xCC))
            {
                if (position + 9 > bytes.Length)
                {
                    break;
                }

                return Sized(PictureFormat.Jpeg, BinaryPrimitives.ReadUInt16BigEndian(bytes[(position + 7)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(position + 5)..]));
            }

            position += 2 + BinaryPrimitives.ReadUInt16BigEndian(bytes[(position + 2)..]);
        }

        throw new InvalidDataException("The JPG file is damaged.");
    }

    private static PictureInfo Sized(PictureFormat format, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidDataException("The picture has no size.");
        }

        return new PictureInfo(format, width, height);
    }
}
