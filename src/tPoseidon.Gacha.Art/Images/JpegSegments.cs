namespace tPoseidon.Gacha.Art.Images;

public static class JpegSegments
{
    public static byte[] Normalize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var output = new MemoryStream(data.Length + 2);
        output.WriteByte(0xFF);
        output.WriteByte(0xD8);
        var position = 0;
        while (position + 1 < data.Length)
        {
            if (data[position] != 0xFF)
            {
                throw new JpegFormatException($"expected a marker at offset {position}");
            }
            var marker = data[position + 1];
            if (marker == 0xFF)
            {
                position++;
                continue;
            }
            if (marker is 0xD8 or 0xD9)
            {
                position += 2;
                continue;
            }
            if (position + 4 > data.Length) throw new JpegFormatException("truncated marker segment");
            var length = (data[position + 2] << 8) | data[position + 3];
            if (marker == 0xDA)
            {
                output.Write(data, position, data.Length - position);
                if (data.Length < 2 || data[^2] != 0xFF || data[^1] != 0xD9)
                {
                    output.WriteByte(0xFF);
                    output.WriteByte(0xD9);
                }
                return output.ToArray();
            }
            if (position + 2 + length > data.Length) throw new JpegFormatException($"segment 0x{marker:X2} runs past the data");
            output.Write(data, position, 2 + length);
            position += 2 + length;
        }
        throw new JpegFormatException("no scan in JPEG data");
    }
}
