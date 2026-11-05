using System;
using System.Linq;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.S2;
using tPoseidon.Core.Net.Social;
using tPoseidon.Kr2014.SystemServer;

static class PartyLifecycleChecks
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new InvalidOperationException(name);
            checks++;
            Console.WriteLine("PASS " + name);
        }
        var service = new PartyService(Encoding.UTF8, new PartyRules(1, TimeSpan.FromMinutes(10)), TimeProvider.System);
        var room = new SocialRoom();
        var server = new PartyServer(service, room, new GameServerLinks("unused", _ => { }), Encoding.UTF8, _ => { });
        SocialMember Member(ulong id) => new(
            (S2SessionContext)RuntimeHelpers.GetUninitializedObject(typeof(S2SessionContext)),
            new Character { CharacterId = id, OwnerAccountId = 1, Slot = 0, Name = "P" + id, Species = default, FType = default, Level = 60 });
        var old = Member(1);
        var other = Member(2);
        server.LoggedInAsync(old, CancellationToken.None).GetAwaiter().GetResult();
        server.LoggedInAsync(other, CancellationToken.None).GetAwaiter().GetResult();
        service.Make(1, "TEST", "P2");
        var joined = service.AcceptInvite(2);
        Check(joined.Any(m => m.Opcode == PartyFrames.JoinState), "initial invitation accepted");
        var replacement = Member(1);
        server.LoggedInAsync(replacement, CancellationToken.None).GetAwaiter().GetResult();
        server.ClosedAsync(old, CancellationToken.None).GetAwaiter().GetResult();
        Check(service.PartyMembers(1).Count == 2, "delayed old close does not log replacement out");
        room.Enter(old);
        room.Enter(replacement);
        Check(room.Leave(old.Context) is null && ReferenceEquals(room.Find(1), replacement), "SocialRoom ignores displaced close");
        room.Leave(replacement.Context);
        server.ClosedAsync(replacement, CancellationToken.None).GetAwaiter().GetResult();
        Check(service.PartyMembers(1).Count == 0 && service.PartyMembers(2).Count == 2, "active close retains offline membership");
        server.ClosedAsync(replacement, CancellationToken.None).GetAwaiter().GetResult();
        Check(service.PartyMembers(2).Count == 2, "duplicate close is idempotent");
        for (var i = 0; i < 50; i++)
        {
            service.Login(1, "P1", 1, 60);
            service.Logout(1);
        }
        Check(service.PartyMembers(2).Count == 2, "50 reconnect cycles preserve roster size");
        var restored = service.Login(1, "P1", 1, 60);
        Check(restored.Orders.Any(o => o.OrderType == SystemLinkFrames.OrderNotifyPartyPlayer && o.Data is not null), "restoration refreshes game-server party mirror");
        Check(service.ChangeDivision(2, PartyDivision.Equal, Array.Empty<PartyDivisionShare>()).Any(m => m.Opcode == PartyFrames.Division), "experience division remains configurable");
        var own = restored.Single(m => m.To == 1 && m.Opcode == PartyFrames.JoinState).Payload;
        var memberId = BitConverter.ToUInt16(own, 9);
        service.Kick(2, memberId);
        Check(service.PartyMembers(1).Count == 0 && service.PartyCount == 0, "kick retains existing under-two dissolution rule");
        service.Make(2, "AGAIN", "P1");
        service.AcceptInvite(1);
        Check(service.PartyMembers(1).Count == 2, "invite works after kick");
        service.Dissolve(2);
        Check(service.PartyCount == 0 && service.PartyMembers(1).Count == 0, "explicit dissolve clears membership");
        Console.WriteLine($"Lifecycle checks: {checks} passed; no real sockets or power interruption simulated.");
    }
}
