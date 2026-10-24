using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public static class LibConfigDatDecryptor
{
    public const string DefaultKey = "#ShowMeTheMoney#";

    private const uint Delta = 0x9E3779B9;
    private const uint InitialSum = 0xC6EF3720;
    private const int Rounds = 32;

    public static byte[] DecryptFile(string datPath, string key = DefaultKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(datPath);
        var bytes = File.ReadAllBytes(datPath);
        return Decrypt(bytes, key);
    }

    public static byte[] Decrypt(ReadOnlySpan<byte> file, string key = DefaultKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        if (file.Length < 1)
        {
            throw new InvalidDataException("libconfig .dat is empty.");
        }

        if (LooksLikePlaintextXml(file))
        {
            var skipBom = file.Length >= 3 && file[0] == 0xEF && file[1] == 0xBB && file[2] == 0xBF;
            return (skipBom ? file[3..] : file).ToArray();
        }

        return DecryptBody(file[1..], key);
    }

    public static byte[] DecryptBody(ReadOnlySpan<byte> ciphertext, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var keyBytes = Encoding.ASCII.GetBytes(key);
        if (keyBytes.Length != 16)
        {
            throw new ArgumentException(
                $"XTEA key must be exactly 16 ASCII bytes; got {keyBytes.Length} for '{key}'.",
                nameof(key));
        }

        var keyWords = new uint[4];
        for (var i = 0; i < 4; i++)
        {
            keyWords[i] = BinaryPrimitives.ReadUInt32BigEndian(keyBytes.AsSpan(i * 4, 4));
        }

        var bodyLen = ciphertext.Length;
        var paddedLen = (bodyLen + 7) & ~7;
        var input = new byte[paddedLen];
        ciphertext.CopyTo(input);

        var output = new byte[paddedLen];
        for (var off = 0; off < paddedLen; off += 8)
        {
            DecryptBlock(input.AsSpan(off, 8), output.AsSpan(off, 8), keyWords);
        }

        return StripNulBytes(output);
    }

    private static bool LooksLikePlaintextXml(ReadOnlySpan<byte> file)
    {
        var s = file;
        if (s.Length >= 3 && s[0] == 0xEF && s[1] == 0xBB && s[2] == 0xBF)
        {
            s = s[3..];
        }
        ReadOnlySpan<byte> xmlDecl = "<?xml"u8;
        ReadOnlySpan<byte> openTag = "<"u8;
        return s.StartsWith(xmlDecl) || s.StartsWith(openTag);
    }

    private static byte[] StripNulBytes(byte[] input)
    {
        var write = 0;
        for (var read = 0; read < input.Length; read++)
        {
            if (input[read] == 0) continue;
            input[write++] = input[read];
        }
        if (write == input.Length) return input;
        var trimmed = new byte[write];
        Array.Copy(input, trimmed, write);
        return trimmed;
    }

    private static void DecryptBlock(ReadOnlySpan<byte> inBlock, Span<byte> outBlock, uint[] key)
    {
        var v0 = BinaryPrimitives.ReadUInt32BigEndian(inBlock[..4]);
        var v1 = BinaryPrimitives.ReadUInt32BigEndian(inBlock.Slice(4, 4));
        var s = InitialSum;
        for (var i = 0; i < Rounds; i++)
        {
            v1 = unchecked(v1 - ((((v0 << 4) ^ (v0 >> 5)) + v0) ^ (s + key[(s >> 11) & 3])));
            s = unchecked(s - Delta);
            v0 = unchecked(v0 - ((((v1 << 4) ^ (v1 >> 5)) + v1) ^ (s + key[s & 3])));
        }
        BinaryPrimitives.WriteUInt32BigEndian(outBlock[..4], v0);
        BinaryPrimitives.WriteUInt32BigEndian(outBlock.Slice(4, 4), v1);
    }

}
