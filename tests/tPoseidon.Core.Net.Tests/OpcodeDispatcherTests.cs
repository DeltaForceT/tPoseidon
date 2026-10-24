using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.Tests;

public sealed class OpcodeDispatcherTests
{
    [Fact]
    public async Task Dispatch_KnownOpcode_InvokesRegisteredHandler()
    {
        var dispatcher = new OpcodeDispatcher();
        var captured = (opcode: (Opcode)0, payload: ReadOnlyMemory<byte>.Empty);

        dispatcher.Register(Opcode.LOGIN_RESPONSE, (op, pl, _) =>
        {
            captured = (op, pl);
            return Task.CompletedTask;
        });

        var body = new byte[] { 0x01, 0x02, 0x03 };
        await dispatcher.DispatchAsync(Opcode.LOGIN_RESPONSE, body);

        Assert.Equal(Opcode.LOGIN_RESPONSE, captured.opcode);
        Assert.Equal(body, captured.payload.ToArray());
    }

    [Fact]
    public async Task Dispatch_UnknownOpcode_NoHandlerRegistered_DropsSilently()
    {
        var dispatcher = new OpcodeDispatcher();
        await dispatcher.DispatchAsync(Opcode.SERVER_BROADCAST, new byte[] { 0xFF });
    }

    [Fact]
    public async Task Dispatch_UnknownOpcode_InvokesUnknownHandler()
    {
        var dispatcher = new OpcodeDispatcher();
        Opcode captured = 0;
        dispatcher.SetUnknownHandler((op, _, _) => { captured = op; return Task.CompletedTask; });

        await dispatcher.DispatchAsync(Opcode.SERVER_BROADCAST, new byte[] { 0xFF });
        Assert.Equal(Opcode.SERVER_BROADCAST, captured);
    }

    [Fact]
    public async Task Dispatch_KnownOpcode_DoesNotInvokeUnknownHandler()
    {
        var dispatcher = new OpcodeDispatcher();
        var knownCount = 0;
        var unknownCount = 0;

        dispatcher.Register(Opcode.CHAR_INFO, (_, _, _) => { knownCount++; return Task.CompletedTask; });
        dispatcher.SetUnknownHandler((_, _, _) => { unknownCount++; return Task.CompletedTask; });

        await dispatcher.DispatchAsync(Opcode.CHAR_INFO, new byte[] { 0x00 });
        Assert.Equal(1, knownCount);
        Assert.Equal(0, unknownCount);
    }

    [Fact]
    public void Register_TwiceSameOpcode_Throws()
    {
        var dispatcher = new OpcodeDispatcher();
        dispatcher.Register(Opcode.CHAR_INFO, (_, _, _) => Task.CompletedTask);
        Assert.Throws<InvalidOperationException>(
            () => dispatcher.Register(Opcode.CHAR_INFO, (_, _, _) => Task.CompletedTask));
    }

    [Fact]
    public void Unregister_ReturnsTrueWhenHandlerExisted()
    {
        var dispatcher = new OpcodeDispatcher();
        dispatcher.Register(Opcode.CHAR_INFO, (_, _, _) => Task.CompletedTask);
        Assert.True(dispatcher.Unregister(Opcode.CHAR_INFO));
        Assert.False(dispatcher.Unregister(Opcode.CHAR_INFO));
    }

    [Fact]
    public async Task Dispatch_HandlerException_Propagates()
    {
        var dispatcher = new OpcodeDispatcher();
        dispatcher.Register(Opcode.CHAR_INFO, (_, _, _) => throw new InvalidOperationException("boom"));
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.DispatchAsync(Opcode.CHAR_INFO, Array.Empty<byte>()));
    }

    [Fact]
    public void RegisteredOpcodes_ReflectsRegistrations()
    {
        var dispatcher = new OpcodeDispatcher();
        Assert.Equal(0, dispatcher.HandlerCount);

        dispatcher.Register(Opcode.LOGIN_RESPONSE, (_, _, _) => Task.CompletedTask);
        dispatcher.Register(Opcode.CHAR_INFO, (_, _, _) => Task.CompletedTask);

        Assert.Equal(2, dispatcher.HandlerCount);
        Assert.Contains(Opcode.LOGIN_RESPONSE, dispatcher.RegisteredOpcodes);
        Assert.Contains(Opcode.CHAR_INFO, dispatcher.RegisteredOpcodes);
    }

    [Fact]
    public async Task Dispatch_PassesCancellationToken()
    {
        var dispatcher = new OpcodeDispatcher();
        var cts = new CancellationTokenSource();
        var receivedToken = CancellationToken.None;

        dispatcher.Register(Opcode.CHAR_INFO, (_, _, ct) =>
        {
            receivedToken = ct;
            return Task.CompletedTask;
        });

        await dispatcher.DispatchAsync(Opcode.CHAR_INFO, Array.Empty<byte>(), cts.Token);
        Assert.Equal(cts.Token, receivedToken);
    }
}
