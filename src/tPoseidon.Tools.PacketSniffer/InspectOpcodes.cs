

using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Tools.PacketSniffer;

internal static class InspectOpcodes
{
    public static void Run(string keyTablePath, string pcapPath, ushort? filterOpcode, int serverPort = 22006, bool clientToServer = false, bool ordered = false)
    {
        var keyBytes = File.ReadAllBytes(keyTablePath);
        if (keyBytes.Length != KeyTable.RequiredLength)
        {
            Console.Error.WriteLine($"KeyTable wrong size: {keyBytes.Length}, want {KeyTable.RequiredLength}");
            return;
        }
        var table = new KeyTable(keyBytes);
        var derivation = new S2v2010Derivation();

        var pcap = File.ReadAllBytes(pcapPath);
        var flows = clientToServer
            ? PcapTcpReassembler.ReassembleClientToServer(pcap, serverPort)
            : PcapTcpReassembler.ReassembleServerToClient(pcap, serverPort);

        

        
        var byOpcode = new Dictionary<ushort, List<byte[]>>();
        var frameIdx = 0;
        foreach (var flow in flows)
        {
            if (flow.Bytes.Length < S2PacketCipher.MinimumFrameSize) continue;
            try { derivation.ValidatePackingFlags(flow.Bytes.AsSpan(0, S2PacketCipher.MinimumFrameSize), table); }
            catch (InvalidPacketException) { continue; }

            var (key, _) = BruteForceInitialKey(table, derivation, flow.Bytes);
            var cipher = new S2PacketCipher(table, derivation);
            var session = new SessionInfo(key) { IsStrict = false };
            var framer = new S2StreamFramer(table, derivation, key);
            framer.Feed(flow.Bytes);

            
            while (framer.TryDrainOne(out var f))
            {
                S2DecryptedPacket dec;
                try { dec = cipher.Decrypt(f, session); }
                catch (InvalidPacketException) { continue; }
                framer.OnSessionKeyAdvanced(session.Key);
                if (filterOpcode is { } want && dec.Opcode != want) continue;
                if (ordered)
                {

                    

                    
                    var head = dec.Payload.Length == 0
                        ? "<empty>"
                        : Convert.ToHexString(dec.Payload.AsSpan(0, Math.Min(16, dec.Payload.Length)));
                    Console.WriteLine($"Frame {frameIdx,4}: opcode=0x{dec.Opcode:X4} len={dec.Payload.Length,4}B head={head}");
                    frameIdx++;
                    continue;
                }
                if (!byOpcode.TryGetValue(dec.Opcode, out var list))
                {
                    list = new List<byte[]>();
                    byOpcode[dec.Opcode] = list;
                }
                list.Add(dec.Payload);
            }
        }
        if (ordered) return;

        if (byOpcode.Count == 0)
        {
            Console.WriteLine("No opcodes matched.");
            return;
        }

        
        foreach (var (op, payloads) in byOpcode.OrderBy(kv => kv.Key))
        {
            var byLen = payloads.GroupBy(p => p.Length).OrderByDescending(g => g.Count());
            Console.WriteLine($"\n========== opcode 0x{op:X4}  ({payloads.Count} samples, {payloads.Select(p => p.Length).Distinct().Count()} distinct lengths) ==========");
            foreach (var group in byLen)
            {
                var len = group.Key;
                var samples = group.ToList();
                Console.WriteLine($"\n  -- length {len} bytes  ({samples.Count} samples) --");

                for (var i = 0; i < samples.Count; i++)
                {
                    var p = samples[i];
                    Console.WriteLine($"    [{i}] {Hex(p)}  |  {Ascii(p)}");
                }

                if (samples.Count >= 2)
                {
                    var variance = new int[len];
                    for (var col = 0; col < len; col++)
                    {
                        var seen = new HashSet<byte>();
                        foreach (var s in samples) seen.Add(s[col]);
                        variance[col] = seen.Count;
                    }
                    Console.Write("    var:");
                    for (var col = 0; col < len; col++)
                    {

                        

                        var v = variance[col];
                        char c = v == 1 ? '.' : v <= 9 ? (char)('0' + v) : '+';
                        Console.Write(' ');
                        Console.Write(c);
                        Console.Write(' ');
                    }
                    Console.WriteLine();
                }
            }
        }
    }

    private static (byte Key, int Frames) BruteForceInitialKey(
        KeyTable table, S2v2010Derivation derivation, byte[] streamBytes)
    {
        byte best = 0;
        var bestN = -1;
        for (var k = 0; k < 256; k++)
        {
            var f = new S2StreamFramer(table, derivation, (byte)k);
            f.Feed(streamBytes);
            var n = f.DrainFrames().Count;
            if (n > bestN) { bestN = n; best = (byte)k; }
        }
        return (best, bestN);
    }

    private static string Hex(byte[] data) =>
        string.Join(' ', data.Select(b => $"{b:X2}"));

    private static string Ascii(byte[] data)
    {
        var chars = new char[data.Length];
        for (var i = 0; i < data.Length; i++)
        {
            var b = data[i];
            chars[i] = b is >= 0x20 and <= 0x7E ? (char)b : '.';
        }
        return new string(chars);
    }
}
