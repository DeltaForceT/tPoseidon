namespace tPoseidon.Core.Crypto;

public sealed class Crc16
{
    public const ushort CcittPolynomial = 0x1021;

    private readonly ushort[] _table = new ushort[256];
    private readonly ushort _initialValue;

    public Crc16(ushort polynomial = CcittPolynomial, ushort initialValue = 0x0000)
    {
        _initialValue = initialValue;
        for (var byteValue = 0; byteValue < 256; byteValue++)
        {
            ushort acc = (ushort)(byteValue << 8);
            for (var bit = 0; bit < 8; bit++)
            {
                var msbSet = (acc & 0x8000) != 0;
                acc <<= 1;
                if (msbSet)
                {
                    acc ^= polynomial;
                }
            }
            _table[byteValue] = acc;
        }
    }

    public ushort Compute(ReadOnlySpan<byte> data)
    {
        var crc = _initialValue;
        foreach (var b in data)
        {
            var index = (byte)((crc >> 8) ^ b);
            crc = (ushort)((crc << 8) ^ _table[index]);
        }
        return crc;
    }
}
