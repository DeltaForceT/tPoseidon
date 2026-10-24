using tPoseidon.Core.Net.Dialogs;

namespace tPoseidon.Core.Net.Tests.Dialogs;

public sealed class NpcScriptRequestTests
{
    [Fact]
    public void AScriptCallIsReadAfterTheInnerOpcodeAndTheEmptyPassword()
    {
        var payload = new byte[]
        {
            0x7F, 0x00, 0x00,
            0x34, 0x12,
            0xEE, 0x00, 0x00, 0x00,
            0x03,
            0x07, 0x00, 0x00, 0x01, 0x0C, 0x00,
        };

        Assert.True(NpcScriptRequest.TryRead(payload, out var request));
        Assert.Equal((ushort)0x1234, request.NpcEntityId);
        Assert.Equal(238u, request.FunctionId);
        Assert.Equal(new ushort[] { 7, 0x100, 12 }, request.Args);
    }

    [Fact]
    public void AFunctionIdAboveOneByteIsKeptWhole()
    {
        var payload = new byte[] { 0x7F, 0x00, 0x00, 0x01, 0x00, 0x05, 0x01, 0x00, 0x00, 0x00 };

        Assert.True(NpcScriptRequest.TryRead(payload, out var request));
        Assert.Equal(261u, request.FunctionId);
        Assert.Empty(request.Args);
    }

    [Fact]
    public void ATypedPasswordIsSkipped()
    {
        var payload = new byte[] { 0x7F, 0x00, (byte)'a', (byte)'b', 0x00, 0x02, 0x00, 0x10, 0x00, 0x00, 0x00, 0x00 };

        Assert.True(NpcScriptRequest.TryRead(payload, out var request));
        Assert.Equal((ushort)2, request.NpcEntityId);
        Assert.Equal(16u, request.FunctionId);
    }

    [Theory]
    [InlineData(new byte[] { 0x43, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 })]
    [InlineData(new byte[] { 0x7F, 0x00, 0x01, 0x02 })]
    [InlineData(new byte[] { 0x7F, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0x00, 0x02, 0x01, 0x00 })]
    public void OtherTunnelMessagesAndShortCallsAreRejected(byte[] payload)
    {
        Assert.False(NpcScriptRequest.TryRead(payload, out _));
    }
}
