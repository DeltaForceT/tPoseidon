using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Angel;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class AngelServiceTests
{
    private const ulong Newbie = 0x3001;
    private const ulong Angel = 0x3002;

    private static readonly Encoding Text = Encoding.Latin1;

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class FakeAngels : IAngelStore
    {
        public List<(ulong Newbie, ulong Angel)> Rows { get; } = new();

        public IReadOnlyList<AngelRelation> Angels(ulong newbieCharacterId) =>
            Rows.Where(row => row.Newbie == newbieCharacterId).Select(row => new AngelRelation(row.Angel, "Ann")).ToList();

        public AngelStoreError Add(ulong newbieCharacterId, ulong angelCharacterId)
        {
            if (Rows.Contains((newbieCharacterId, angelCharacterId))) return AngelStoreError.AlreadyListed;
            Rows.Add((newbieCharacterId, angelCharacterId));
            return AngelStoreError.None;
        }

        public bool Remove(ulong newbieCharacterId, ulong angelCharacterId) => Rows.Remove((newbieCharacterId, angelCharacterId));

        public int GiftRows { get; set; } = 1;

        public Dictionary<ulong, AngelResult> Results { get; } = new();

        public AngelResult? Result(ulong angelCharacterId) => Results.GetValueOrDefault(angelCharacterId);

        public AngelGraduation Graduate(ulong newbieCharacterId, uint newbieAccountId, DateTimeOffset at)
        {
            var angels = Rows.Where(row => row.Newbie == newbieCharacterId).Select(row => row.Angel).ToList();
            if (angels.Count == 0 || GiftRows == 0) return new AngelGraduation(false, angels, 0);
            foreach (var angel in angels) Results[angel] = new AngelResult((Results.GetValueOrDefault(angel)?.GraduationCount ?? 0) + 1, at);
            Rows.RemoveAll(row => row.Newbie == newbieCharacterId);
            return new AngelGraduation(true, angels, GiftRows);
        }
    }

    [Fact]
    public void GraduationAtLevel31RewardsEveryListedAngel()
    {
        var store = new FakeAngels();
        store.Rows.Add((Newbie, Angel));
        var service = new AngelService(store, Text, new Clock());
        Assert.Empty(service.LevelChanged(Newbie, 7, 30).Orders);
        var graduated = service.LevelChanged(Newbie, 7, 31);
        var order = Assert.Single(graduated.Orders);
        Assert.Equal((Angel, AngelFrames.OrderAngelReward), (order.CharacterId, order.OrderType));
        Assert.Equal(1, store.Results[Angel].GraduationCount);
        Assert.Empty(service.LevelChanged(Newbie, 7, 32).Orders);
    }

    [Fact]
    public void AnEmptyGiftTableFailsTheGraduation()
    {
        var store = new FakeAngels { GiftRows = 0 };
        store.Rows.Add((Newbie, Angel));
        var outcome = new AngelService(store, Text, new Clock()).LevelChanged(Newbie, 7, 31);
        Assert.Empty(outcome.Orders);
        Assert.Single(outcome.Notes);
        Assert.Single(store.Rows);
    }

    [Fact]
    public void WingLastsSevenDays()
    {
        var at = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.True(AngelRules.WingActive(at, at.AddSeconds(604799)));
        Assert.False(AngelRules.WingActive(at, at.AddSeconds(604800)));
    }

    [Fact]
    public void CallAcceptMoveAndReturn()
    {
        var store = new FakeAngels();
        var clock = new Clock();
        var service = new AngelService(store, Text, clock);
        Assert.Equal(AngelFrames.SessionOpen, Assert.Single(service.LoggedIn(Newbie, 12).Messages).Opcode);
        Assert.Empty(service.LoggedIn(Angel, 60).Messages);

        var call = service.Call(Newbie, "Nu", 12, 100, new AngelCall(AngelRules.RequestNextFree, 0, "help"));
        var toAngel = Assert.Single(call.Messages);
        Assert.Equal((Angel, AngelFrames.CallToAngel), (toAngel.To, toAngel.Opcode));
        Assert.Equal(new byte[] { (byte)'N', (byte)'u', 0, 12, 0, 100, 0, 0, (byte)'h', (byte)'e', (byte)'l', (byte)'p', 0 }, toAngel.Payload);

        var busy = service.Call(Newbie, "Nu", 12, 100, new AngelCall(AngelRules.RequestListed, Angel, "again"));
        Assert.Equal(new byte[] { AngelRules.SameAngelTooSoon }, Assert.Single(busy.Messages).Payload);

        var accept = service.Accept(Angel, 7, 10, 20);
        var order = Assert.Single(accept.Orders);
        Assert.Equal((Newbie, AngelFrames.OrderNewbiePosition), (order.CharacterId, order.OrderType));

        var moved = service.NewbieSpot(new AngelSpot(Angel, 100, 50, 60), _ => "Ann");
        Assert.Equal(AngelFrames.OrderAngelMove, Assert.Single(moved.Orders).OrderType);
        Assert.Contains(moved.Messages, m => m.To == Newbie && m.Opcode == AngelFrames.Listed);
        Assert.Equal([(Newbie, Angel)], store.Rows);

        var back = service.Portal(Angel, goBack: true, rude: false);
        var direct = Assert.Single(back.Direct);
        Assert.Equal(new byte[] { 2, 0x30, 0, 0, 0, 0, 0, 0, 10, 0, 20, 0, 7, 0 }, direct.Payload);

        var helping = service.Call(Newbie, "Nu", 12, 100, new AngelCall(AngelRules.RequestListed, Angel, "x"));
        Assert.Equal(new byte[] { AngelRules.HelpingAnother }, Assert.Single(helping.Messages).Payload);
    }

    [Fact]
    public void RudeDismissBlocksTheNewbie()
    {
        var store = new FakeAngels();
        var clock = new Clock();
        var service = new AngelService(store, Text, clock);
        service.LoggedIn(Newbie, 5);
        service.LoggedIn(Angel, 90);
        service.Call(Newbie, "Nu", 5, 1, new AngelCall(AngelRules.RequestNextFree, 0, string.Empty));
        var dismissed = service.Dismiss(Angel, rude: true);
        Assert.Equal(2, dismissed.Messages.Count(m => m.To == Newbie && m.Opcode == AngelFrames.Refused));
        clock.Now = clock.Now.AddSeconds(100);
        var blocked = service.Call(Newbie, "Nu", 5, 1, new AngelCall(AngelRules.RequestNextFree, 0, string.Empty));
        var refusal = Assert.Single(blocked.Messages).Payload;
        Assert.Equal(AngelRules.RudeNewbie, refusal[0]);
        Assert.Equal(200, BitConverter.ToUInt16(refusal, 1));
    }

    [Fact]
    public void ListEntriesAreThirtyTwoBytes()
    {
        var bytes = AngelFrames.ListOf([new AngelRelation(9, "Ann")], Text);
        Assert.Equal(4 + 32, bytes.Length);
        Assert.Equal(1U, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(9UL, BitConverter.ToUInt64(bytes, 4));
        Assert.Equal("Ann", Text.GetString(bytes, 12, 3));
        Assert.All(bytes.Skip(15), b => Assert.Equal(0, b));
    }
}
