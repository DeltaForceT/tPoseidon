using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public sealed record LibConfigEncoded(byte[] Dat, byte[] Idx, int TableCount);

public static class LibConfigDatEncryptor
{
    private static readonly byte[] TableLinePrefix = "	<TABLE name='"u8.ToArray();

    public const byte FormatMarker = 0x01;
    public const int BlockBytes = 8;

    private const uint Delta = 0x9E3779B9;
    private const int Rounds = 32;
    private const byte LineFeed = (byte)'\n';
    private const byte NameQuote = (byte)'\'';

    public static LibConfigEncoded Encode(ReadOnlySpan<byte> xml, string key = LibConfigDatDecryptor.DefaultKey)
    {
        var padded = PadLines(xml);
        var dat = EncryptPadded(padded, key);
        var idx = new MemoryStream();
        var tables = 0;
        var lineStart = 0;
        var span = xml;
        var paddedOffset = 0;
        Span<byte> offset = stackalloc byte[4];
        while (lineStart < span.Length)
        {
            var feed = span[lineStart..].IndexOf(LineFeed);
            var length = feed < 0 ? span.Length - lineStart : feed + 1;
            var line = span.Slice(lineStart, length);
            if (line.StartsWith(TableLinePrefix))
            {
                var nameStart = TableLinePrefix.Length;
                var nameLength = line[nameStart..].IndexOf(NameQuote);
                idx.Write(line.Slice(nameStart, nameLength));
                idx.WriteByte(0);
                BinaryPrimitives.WriteUInt32LittleEndian(offset, (uint)(paddedOffset + 1));
                idx.Write(offset);
                tables++;
            }

            paddedOffset += length + BlockBytes - length % BlockBytes;
            lineStart += length;
        }

        return new LibConfigEncoded(dat, idx.ToArray(), tables);
    }

    public static byte[] PadLines(ReadOnlySpan<byte> xml)
    {
        var output = new List<byte>(xml.Length + xml.Length / 8);
        var lineStart = 0;
        for (var i = 0; i < xml.Length; i++)
        {
            if (xml[i] != LineFeed)
            {
                continue;
            }

            AppendLine(output, xml[lineStart..(i + 1)]);
            lineStart = i + 1;
        }

        if (lineStart < xml.Length)
        {
            AppendLine(output, xml[lineStart..]);
        }

        return output.ToArray();
    }

    public static byte[] Encrypt(ReadOnlySpan<byte> xml, string key = LibConfigDatDecryptor.DefaultKey)
    {
        return EncryptPadded(PadLines(xml), key);
    }

    public static byte[] EncryptPadded(ReadOnlySpan<byte> padded, string key = LibConfigDatDecryptor.DefaultKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var keyBytes = Encoding.ASCII.GetBytes(key);
        if (keyBytes.Length != 16)
        {
            throw new ArgumentException($"XTEA key must be exactly 16 ASCII bytes; got {keyBytes.Length}.", nameof(key));
        }

        if (padded.Length % BlockBytes != 0)
        {
            throw new ArgumentException("Padded plaintext must be a multiple of 8 bytes.", nameof(padded));
        }

        var keyWords = new uint[4];
        for (var i = 0; i < 4; i++)
        {
            keyWords[i] = BinaryPrimitives.ReadUInt32BigEndian(keyBytes.AsSpan(i * 4, 4));
        }

        var output = new byte[padded.Length + 1];
        output[0] = FormatMarker;
        for (var off = 0; off < padded.Length; off += BlockBytes)
        {
            EncryptBlock(padded.Slice(off, BlockBytes), output.AsSpan(1 + off, BlockBytes), keyWords);
        }

        return output;
    }

    public static void EncryptFile(string datPath, ReadOnlySpan<byte> xml, string key = LibConfigDatDecryptor.DefaultKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(datPath);
        File.WriteAllBytes(datPath, Encrypt(xml, key));
    }

    private static void AppendLine(List<byte> output, ReadOnlySpan<byte> line)
    {
        foreach (var b in line)
        {
            output.Add(b);
        }

        var padding = BlockBytes - line.Length % BlockBytes;
        for (var i = 0; i < padding; i++)
        {
            output.Add(0);
        }
    }

    private static void EncryptBlock(ReadOnlySpan<byte> inBlock, Span<byte> outBlock, uint[] key)
    {
        var v0 = BinaryPrimitives.ReadUInt32BigEndian(inBlock[..4]);
        var v1 = BinaryPrimitives.ReadUInt32BigEndian(inBlock.Slice(4, 4));
        var s = 0u;
        for (var i = 0; i < Rounds; i++)
        {
            v0 = unchecked(v0 + ((((v1 << 4) ^ (v1 >> 5)) + v1) ^ (s + key[s & 3])));
            s = unchecked(s + Delta);
            v1 = unchecked(v1 + ((((v0 << 4) ^ (v0 >> 5)) + v0) ^ (s + key[(s >> 11) & 3])));
        }

        BinaryPrimitives.WriteUInt32BigEndian(outBlock[..4], v0);
        BinaryPrimitives.WriteUInt32BigEndian(outBlock.Slice(4, 4), v1);
    }
}
