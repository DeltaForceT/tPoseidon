using System;
using System.Linq;
using System.Text;
using tPoseidon.Core.Net.Social;

static class PartyCustomDivisionChecks
{
    public static void Run()
    {
        var party = new PartyService(Encoding.UTF8, new PartyRules(1, TimeSpan.FromMinutes(10)), TimeProvider.System);
        party.Login(1, "P1", 1, 60);
        party.Login(2, "P2", 1, 80);
        party.Make(1, "CUSTOM", "P2");
        var joined = party.AcceptInvite(2);
        var second = BitConverter.ToUInt16(joined.Single(m => m.To == 2 && m.Opcode == PartyFrames.JoinState).Payload, 9);
        var first = BitConverter.ToUInt16(joined.Single(m => m.To == 2 && m.Opcode == PartyFrames.JoinState).Payload, 7);
        var expected = PartyFrames.DivisionFor(PartyDivision.Custom,
            new[] { new PartyDivisionShare(first, 30), new PartyDivisionShare(second, 70) });
        party.ChangeDivision(1, PartyDivision.Custom,
            new[] { new PartyDivisionShare(first, 30), new PartyDivisionShare(second, 70) });
        var count = 0;
        void Check(PartyOutbox box, ulong id, string name)
        {
            if (!box.Any(m => m.To == id && m.Opcode == PartyFrames.JoinState)
                || !box.Any(m => m.To == id && m.Opcode == PartyFrames.Division && m.Payload.SequenceEqual(expected)))
                throw new InvalidOperationException(name);
            count++;
            Console.WriteLine("PASS Custom 30/70 preserved: " + name);
        }
        Check(party.Login(2, "P2", 1, 80), 2, "already-online reconnect");
        party.Logout(2);
        Check(party.Login(2, "P2", 1, 80), 2, "member reconnect");
        party.Logout(1);
        Check(party.Login(1, "P1", 1, 60), 1, "former leader reconnect");
        party.Logout(1);
        party.Logout(2);
        Check(party.Login(1, "P1", 1, 60), 1, "first return from all-offline");
        Check(party.Login(2, "P2", 1, 80), 2, "last return from all-offline");
        Console.WriteLine($"Custom division checks: {count} passed.");
    }
}
