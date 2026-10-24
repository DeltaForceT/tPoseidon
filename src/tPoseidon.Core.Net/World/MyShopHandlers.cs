using System.Text;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net.World;

public readonly record struct MyShopReturn(ushort Zone, ushort X, ushort Y);

public static class MyShopHandlers
{
    public static void Register(
        OpcodeDispatcher dispatcher,
        Func<Opcode, byte[], CancellationToken, Task> send,
        Func<MyShopReturn?> locate,
        Func<Task> depart,
        Func<Task> restore,
        Encoding encoding,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(locate);
        ArgumentNullException.ThrowIfNull(depart);
        ArgumentNullException.ThrowIfNull(restore);
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentNullException.ThrowIfNull(log);

        dispatcher.Register(Opcode.C2S_MYSHOP_OPEN, async (_, _, ct) =>
        {
            log("[myshop] open request; answering 0x01AD");
            await depart().ConfigureAwait(false);
            await send(Opcode.S2C_MYSHOP_OPENED, Array.Empty<byte>(), ct).ConfigureAwait(false);
        });
        var inPurchaseStorage = false;
        dispatcher.Register(Opcode.C2S_PURCHASE_STORAGE_MODE, (_, _, _) =>
        {
            inPurchaseStorage = true;
            log("[myshop] purchase storage mode");
            return Task.CompletedTask;
        });
        dispatcher.Register(Opcode.C2S_PURCHASE_STORAGE_LEAVE, (_, _, _) =>
        {
            if (!inPurchaseStorage) return Task.CompletedTask;
            inPurchaseStorage = false;
            log("[myshop] purchase storage mode left");
            return Task.CompletedTask;
        });
        dispatcher.Register(Opcode.C2S_MYSHOP_ID_LIST, (_, _, ct) =>
            send(Opcode.S2C_MYSHOP_ID_LIST, MyShopFrames.EmptyList(), ct));
        dispatcher.Register(Opcode.C2S_BOUGHT_LIST, (_, _, ct) =>
            send(Opcode.S2C_BOUGHT_LIST, MyShopFrames.EmptyList(), ct));
        dispatcher.Register(Opcode.C2S_BOUGHT_LIST_CAMP, (_, _, ct) =>
            send(Opcode.S2C_BOUGHT_LIST_CAMP, MyShopFrames.EmptyList(), ct));
        dispatcher.Register(Opcode.C2S_MYSHOP_ENTER, (_, _, ct) =>
            send(Opcode.S2C_MYSHOP_ENTERED, MyShopFrames.Entered(string.Empty, encoding), ct));
        dispatcher.Register(Opcode.C2S_MYSHOP_CLOSE, async (_, _, ct) =>
        {
            if (locate() is not { } back) return;
            log($"[myshop] close; back to map {back.Zone} ({back.X},{back.Y})");
            await depart().ConfigureAwait(false);
            await send(Opcode.S2C_MYSHOP_LEAVE, MyShopFrames.Leave(back.Zone, back.X, back.Y), ct).ConfigureAwait(false);
        });
        dispatcher.Register(Opcode.C2S_MYSHOP_LEFT, async (_, _, ct) =>
        {
            await send(Opcode.S2C_MYSHOP_LEFT, Array.Empty<byte>(), ct).ConfigureAwait(false);
            await restore().ConfigureAwait(false);
        });
    }
}
