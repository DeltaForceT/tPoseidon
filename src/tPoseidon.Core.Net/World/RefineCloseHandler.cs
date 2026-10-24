using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public static class RefineCloseHandler
{
    public static void Register(
        OpcodeDispatcher dispatcher,
        Func<Opcode, byte[], CancellationToken, Task> send,
        Action leave)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(leave);

        dispatcher.Register(Opcode.REFINE_CLOSE, async (_, _, ct) =>
        {
            leave();
            await send(Opcode.S2C_REFINE_CLOSED, Array.Empty<byte>(), ct).ConfigureAwait(false);
        });
    }
}
