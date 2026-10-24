using tPoseidon.Core.Common;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class RefineCloseHandlerTests
{
    private sealed class Harness
    {
        public OpcodeDispatcher Dispatcher { get; } = new();
        public List<string> Events { get; } = [];

        public Harness()
        {
            RefineCloseHandler.Register(
                Dispatcher,
                (opcode, payload, _) =>
                {
                    Events.Add($"send {opcode} {payload.Length}");
                    return Task.CompletedTask;
                },
                () => Events.Add("leave"));
        }

        public Task Send(Opcode opcode) => Dispatcher.DispatchAsync(opcode, ReadOnlyMemory<byte>.Empty);
    }

    [Fact]
    public void ClosedAckIsTheOpcodeAfterTheCloseRequest()
    {
        Assert.Equal((ushort)Opcode.REFINE_CLOSE + 1, (ushort)Opcode.S2C_REFINE_CLOSED);
    }

    [Fact]
    public async Task ClosingTheRefineWindowLeavesTheStateThenAnswersWithAnEmptyClosedFrame()
    {
        var refine = new Harness();

        await refine.Send(Opcode.REFINE_CLOSE);

        Assert.Equal(["leave", "send S2C_REFINE_CLOSED 0"], refine.Events);
    }

    [Fact]
    public async Task EveryOpenAndCloseRoundIsAnswered()
    {
        var refine = new Harness();

        for (var round = 0; round < 3; round++) await refine.Send(Opcode.REFINE_CLOSE);

        Assert.Equal(3, refine.Events.Count(e => e == "send S2C_REFINE_CLOSED 0"));
        Assert.Equal(3, refine.Events.Count(e => e == "leave"));
    }
}
