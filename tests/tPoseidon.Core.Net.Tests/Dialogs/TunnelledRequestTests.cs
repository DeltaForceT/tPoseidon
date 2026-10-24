using tPoseidon.Core.Net.Dialogs;

namespace tPoseidon.Core.Net.Tests.Dialogs;

public sealed class TunnelledRequestTests
{
    [Fact]
    public void TheInnerOpcodeAndTheBodyAfterAnEmptyPasswordAreFound()
    {
        var payload = new byte[] { 0x4C, 0x00, 0x00, 0x34, 0x12, 0x01, 0x00 };

        Assert.True(TunnelledRequest.TrySplit(payload, out var innerOpcode, out var bodyOffset));
        Assert.Equal((ushort)0x004C, innerOpcode);
        Assert.Equal(3, bodyOffset);
    }

    [Fact]
    public void ATypedPasswordIsSkipped()
    {
        var payload = new byte[] { 0x4C, 0x00, (byte)'p', (byte)'w', 0x00, 0x34, 0x12 };

        Assert.True(TunnelledRequest.TrySplit(payload, out var innerOpcode, out var bodyOffset));
        Assert.Equal((ushort)0x004C, innerOpcode);
        Assert.Equal(5, bodyOffset);
    }

    [Theory]
    [InlineData(new byte[] { 0x4C })]
    [InlineData(new byte[] { 0x4C, 0x00, (byte)'p' })]
    public void APayloadWithoutAnEndedPasswordIsRejected(byte[] payload)
    {
        Assert.False(TunnelledRequest.TrySplit(payload, out _, out _));
    }

    [Fact]
    public void AnEmptyPayloadIsRejected()
    {
        Assert.False(TunnelledRequest.TrySplit(ReadOnlySpan<byte>.Empty, out _, out _));
    }
}
