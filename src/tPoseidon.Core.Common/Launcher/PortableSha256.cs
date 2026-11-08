using System.Buffers.Binary;

namespace tPoseidon.Core.Common.Launcher;

internal static class PortableSha256
{
    public const int DigestBytes = 32;
    private const int BlockBytes = 64;

    private static readonly uint[] RoundConstants =
    [
        0x428a2f98, 0x71374491, 0xb5c0fbcf, 0xe9b5dba5, 0x3956c25b, 0x59f111f1, 0x923f82a4, 0xab1c5ed5,
        0xd807aa98, 0x12835b01, 0x243185be, 0x550c7dc3, 0x72be5d74, 0x80deb1fe, 0x9bdc06a7, 0xc19bf174,
        0xe49b69c1, 0xefbe4786, 0x0fc19dc6, 0x240ca1cc, 0x2de92c6f, 0x4a7484aa, 0x5cb0a9dc, 0x76f988da,
        0x983e5152, 0xa831c66d, 0xb00327c8, 0xbf597fc7, 0xc6e00bf3, 0xd5a79147, 0x06ca6351, 0x14292967,
        0x27b70a85, 0x2e1b2138, 0x4d2c6dfc, 0x53380d13, 0x650a7354, 0x766a0abb, 0x81c2c92e, 0x92722c85,
        0xa2bfe8a1, 0xa81a664b, 0xc24b8b70, 0xc76c51a3, 0xd192e819, 0xd6990624, 0xf40e3585, 0x106aa070,
        0x19a4c116, 0x1e376c08, 0x2748774c, 0x34b0bcb5, 0x391c0cb3, 0x4ed8aa4a, 0x5b9cca4f, 0x682e6ff3,
        0x748f82ee, 0x78a5636f, 0x84c87814, 0x8cc70208, 0x90befffa, 0xa4506ceb, 0xbef9a3f7, 0xc67178f2,
    ];

    public static byte[] Hash(ReadOnlySpan<byte> data)
    {
        uint[] state = [0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19];
        var padded = new byte[(data.Length + 9 + BlockBytes - 1) / BlockBytes * BlockBytes];
        data.CopyTo(padded);
        padded[data.Length] = 0x80;
        BinaryPrimitives.WriteUInt64BigEndian(padded.AsSpan(padded.Length - 8), (ulong)data.Length * 8);
        var schedule = new uint[64];
        for (var offset = 0; offset < padded.Length; offset += BlockBytes)
        {
            Compress(state, padded.AsSpan(offset, BlockBytes), schedule);
        }

        var digest = new byte[DigestBytes];
        for (var index = 0; index < state.Length; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(digest.AsSpan(index * 4), state[index]);
        }

        return digest;
    }

    public static byte[] Hmac(byte[] key, byte[] message)
    {
        var block = new byte[BlockBytes];
        (key.Length > BlockBytes ? Hash(key) : key).CopyTo(block, 0);
        var inner = new byte[BlockBytes + message.Length];
        var outer = new byte[BlockBytes + DigestBytes];
        for (var index = 0; index < BlockBytes; index++)
        {
            inner[index] = (byte)(block[index] ^ 0x36);
            outer[index] = (byte)(block[index] ^ 0x5c);
        }

        message.CopyTo(inner, BlockBytes);
        Hash(inner).CopyTo(outer, BlockBytes);
        return Hash(outer);
    }

    private static void Compress(uint[] state, ReadOnlySpan<byte> block, uint[] w)
    {
        for (var index = 0; index < 16; index++)
        {
            w[index] = BinaryPrimitives.ReadUInt32BigEndian(block[(index * 4)..]);
        }

        for (var index = 16; index < 64; index++)
        {
            var s0 = uint.RotateRight(w[index - 15], 7) ^ uint.RotateRight(w[index - 15], 18) ^ (w[index - 15] >> 3);
            var s1 = uint.RotateRight(w[index - 2], 17) ^ uint.RotateRight(w[index - 2], 19) ^ (w[index - 2] >> 10);
            w[index] = w[index - 16] + s0 + w[index - 7] + s1;
        }

        var a = state[0];
        var b = state[1];
        var c = state[2];
        var d = state[3];
        var e = state[4];
        var f = state[5];
        var g = state[6];
        var h = state[7];
        for (var index = 0; index < 64; index++)
        {
            var sum1 = uint.RotateRight(e, 6) ^ uint.RotateRight(e, 11) ^ uint.RotateRight(e, 25);
            var choose = (e & f) ^ (~e & g);
            var temp1 = h + sum1 + choose + RoundConstants[index] + w[index];
            var sum0 = uint.RotateRight(a, 2) ^ uint.RotateRight(a, 13) ^ uint.RotateRight(a, 22);
            var majority = (a & b) ^ (a & c) ^ (b & c);
            var temp2 = sum0 + majority;
            h = g;
            g = f;
            f = e;
            e = d + temp1;
            d = c;
            c = b;
            b = a;
            a = temp1 + temp2;
        }

        state[0] += a;
        state[1] += b;
        state[2] += c;
        state[3] += d;
        state[4] += e;
        state[5] += f;
        state[6] += g;
        state[7] += h;
    }
}
