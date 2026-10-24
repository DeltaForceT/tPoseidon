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
        Encoding encoding,
        Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(send);
        ArgumentNullException.ThrowIfNull(locate);
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentNullException.ThrowIfNull(log);

        dispatcher.Register(Opcode.C2S_MYSHOP_OPEN, (_, _, ct) =>
        {
            log("[myshop] open request; answering 0x01AD");
            return send(Opcode.S2C_MYSHOP_OPENED, Array.Empty<byte>(), ct);
        });
        dispatcher.Register(Opcode.C2S_PURCHASE_STORAGE_MODE, (_, _, _) =>
        {
            log("[myshop] purchase storage mode");
            return Task.CompletedTask;
        });
        dispatcher.Register(Opcode.C2S_PURCHASE_STORAGE_LEAVE, (_, _, _) =>
        {
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
        dispatcher.Register(Opcode.C2S_MYSHOP_CLOSE, (_, _, ct) =>
        {
            if (locate() is not { } back) return Task.CompletedTask;
            log($"[myshop] close; back to map {back.Zone} ({back.X},{back.Y})");
            return send(Opcode.S2C_MYSHOP_LEAVE, MyShopFrames.Leave(back.Zone, back.X, back.Y), ct);
        });
        dispatcher.Register(Opcode.C2S_MYSHOP_LEFT, (_, _, ct) =>
            send(Opcode.S2C_MYSHOP_LEFT, Array.Empty<byte>(), ct));
    }
}
