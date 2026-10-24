using tPoseidon.Core.Crypto;
using Xunit;

namespace tPoseidon.Core.Crypto.Tests;

public sealed class Crc16Tests
{
    [Fact]
    public void EmptyInput_ReturnsInitialValue()
    {
        var crc = new Crc16(initialValue: 0x1234);
        Assert.Equal(0x1234, crc.Compute(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void KnownCcittVector_ABCDEFGHI()
    {
        var crc = new Crc16(polynomial: Crc16.CcittPolynomial, initialValue: 0xFFFF);
        var result = crc.Compute("123456789"u8);
        Assert.Equal(0x29B1, result);
    }

    [Fact]
    public void SameInput_SameOutput()
    {
        var crc = new Crc16();
        var data = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE };
        Assert.Equal(crc.Compute(data), crc.Compute(data));
    }

    [Fact]
    public void DifferentInput_DifferentOutput_UsuallyTrue()
    {
        var crc = new Crc16();
        Assert.NotEqual(crc.Compute([0x00, 0x00]), crc.Compute([0x01, 0x00]));
    }
}
