namespace tPoseidon.Core.Crypto.S2;

public sealed class KeyTable
{
    public const int RequiredLength = 65536;

    private readonly byte[] _bytes;

    public KeyTable(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != RequiredLength)
        {
            throw new ArgumentException(
                $"KeyTable must be exactly {RequiredLength} bytes; got {bytes.Length}.",
                nameof(bytes));
        }
        _bytes = (byte[])bytes.Clone();
    }

    private readonly byte[]?[] _inverseRows = new byte[]?[256];

        public byte Lookup(int index) => _bytes[index & 0xFFFF];

        public byte InverseLookup(int row, byte plaintext)
    {
        var rowMasked = row & 0xFF;
        var inverse = _inverseRows[rowMasked];
        if (inverse is null)
        {
            inverse = BuildInverseRow(rowMasked);
            _inverseRows[rowMasked] = inverse;
        }
        return inverse[plaintext];
    }

    private byte[] BuildInverseRow(int row)
    {
        var inverse = new byte[256];
        var baseOffset = row << 8;
        for (var c = 0; c < 256; c++)
        {
            var p = _bytes[baseOffset | c];
            inverse[p] = (byte)c;
        }
        return inverse;
    }

        public static KeyTable Identity()
    {
        var bytes = new byte[RequiredLength];
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] = (byte)i;
        }
        return new KeyTable(bytes);
    }

        public static KeyTable Dummy(int seed)
    {
        var rng = new Random(seed);
        var bytes = new byte[RequiredLength];
        for (var page = 0; page < 256; page++)
        {
            var perm = new byte[256];
            for (var i = 0; i < 256; i++) perm[i] = (byte)i;
            for (var i = 255; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }
            Buffer.BlockCopy(perm, 0, bytes, page * 256, 256);
        }
        return new KeyTable(bytes);
    }
}
