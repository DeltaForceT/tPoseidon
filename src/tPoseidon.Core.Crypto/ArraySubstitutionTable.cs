namespace tPoseidon.Core.Crypto;

public sealed class ArraySubstitutionTable : ISubstitutionTable
{
    public const int RequiredLength = 4096;

    private readonly byte[] _bytes;

    public ArraySubstitutionTable(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != RequiredLength)
        {
            throw new ArgumentException(
                $"Substitution table must be exactly {RequiredLength} bytes; got {bytes.Length}.",
                nameof(bytes));
        }

        _bytes = (byte[])bytes.Clone();
    }

    public int Length => _bytes.Length;

    public byte Substitute(byte keyNibble, byte value)
    {
        if (keyNibble > 0x0F)
        {
            throw new ArgumentOutOfRangeException(nameof(keyNibble), "Key nibble must be 0..15.");
        }

        return _bytes[(keyNibble << 8) | value];
    }

        public static ArraySubstitutionTable Identity()
    {
        var bytes = new byte[RequiredLength];
        for (var row = 0; row < 16; row++)
        {
            for (var col = 0; col < 256; col++)
            {
                bytes[(row << 8) | col] = (byte)col;
            }
        }
        return new ArraySubstitutionTable(bytes);
    }

        public static (ArraySubstitutionTable Encrypt, ArraySubstitutionTable Decrypt) GenerateDummyPair(int seed)
    {
        var rng = new Random(seed);
        var encBytes = new byte[RequiredLength];
        var decBytes = new byte[RequiredLength];

        for (var row = 0; row < 16; row++)
        {
            var perm = new byte[256];
            for (var i = 0; i < 256; i++)
            {
                perm[i] = (byte)i;
            }

            for (var i = 255; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }

            for (var col = 0; col < 256; col++)
            {
                encBytes[(row << 8) | col] = perm[col];
                decBytes[(row << 8) | perm[col]] = (byte)col;
            }
        }

        return (new ArraySubstitutionTable(encBytes), new ArraySubstitutionTable(decBytes));
    }
}
