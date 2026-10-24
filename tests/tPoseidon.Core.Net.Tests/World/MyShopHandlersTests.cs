using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class MyShopHandlersTests
{
    private sealed class Harness
    {
        public OpcodeDispatcher Dispatcher { get; } = new();
        public List<(Opcode Opcode, byte[] Payload)> Sent { get; } = [];
        public MyShopReturn? Position { get; set; } = new MyShopReturn(148, 949, 2228);
        public List<string> Log { get; } = [];

        public Harness()
        {
            MyShopHandlers.Register(
                Dispatcher,
                (opcode, payload, _) =>
                {
                    Sent.Add((opcode, payload));
                    return Task.CompletedTask;
                },
                () => Position,
                Encoding.ASCII,
                Log.Add);
        }

        public Task Send(Opcode opcode) => Dispatcher.DispatchAsync(opcode, ReadOnlyMemory<byte>.Empty);
    }

    private static readonly byte[] ReturnTo148 = [148, 0, 0xB5, 0x03, 0xB4, 0x08];

    [Fact]
    public async Task OpenCloseReopenCloseAnswersEveryRound()
    {
        var shop = new Harness();

        for (var round = 0; round < 2; round++)
        {
            await shop.Send(Opcode.C2S_MYSHOP_OPEN);
            await shop.Send(Opcode.C2S_PURCHASE_STORAGE_MODE);
            await shop.Send(Opcode.C2S_MYSHOP_CLOSE);
            await shop.Send(Opcode.C2S_MYSHOP_LEFT);
            await shop.Send(Opcode.C2S_PURCHASE_STORAGE_LEAVE);
        }

        Assert.Equal(
            [Opcode.S2C_MYSHOP_OPENED, Opcode.S2C_MYSHOP_LEAVE, Opcode.S2C_MYSHOP_LEFT,
             Opcode.S2C_MYSHOP_OPENED, Opcode.S2C_MYSHOP_LEAVE, Opcode.S2C_MYSHOP_LEFT],
            shop.Sent.Select(frame => frame.Opcode));
        Assert.Equal(ReturnTo148, shop.Sent[1].Payload);
        Assert.Equal(ReturnTo148, shop.Sent[4].Payload);
        Assert.Empty(shop.Sent[0].Payload);
        Assert.Empty(shop.Sent[2].Payload);
    }

    [Fact]
    public async Task LeavingPurchaseStorageDoesNotReleaseThePlayer()
    {
        var shop = new Harness();

        await shop.Send(Opcode.C2S_PURCHASE_STORAGE_MODE);
        await shop.Send(Opcode.C2S_PURCHASE_STORAGE_LEAVE);
        await shop.Send(Opcode.C2S_MYSHOP_OPEN);
        await shop.Send(Opcode.C2S_MYSHOP_CLOSE);

        Assert.Equal([Opcode.S2C_MYSHOP_OPENED, Opcode.S2C_MYSHOP_LEAVE], shop.Sent.Select(frame => frame.Opcode));
        Assert.Contains("[myshop] purchase storage mode left", shop.Log);
    }

    [Fact]
    public async Task CloseOutsideTheWorldSendsNothing()
    {
        var shop = new Harness { Position = null };

        await shop.Send(Opcode.C2S_MYSHOP_CLOSE);

        Assert.Empty(shop.Sent);
    }

    [Fact]
    public void RegisteringTheDisconnectOpcodeAgainIsRefused()
    {
        var shop = new Harness();

        Assert.Throws<InvalidOperationException>(() =>
            shop.Dispatcher.Register(Opcode.C2S_PURCHASE_STORAGE_LEAVE, (_, _, _) => Task.CompletedTask));
    }
}
