

using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Tools.PacketSniffer;

internal static class DecryptCapture
{
    public static void Run(string keyTablePath, string pcapPath, int serverPort = 22006, bool clientToServer = false)
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
        var directionLabel = clientToServer ? "C→S" : "S→C";
        var flows = clientToServer
            ? PcapTcpReassembler.ReassembleClientToServer(pcap, serverPort)
            : PcapTcpReassembler.ReassembleServerToClient(pcap, serverPort);
        Console.WriteLine($"Reassembled {flows.Count} {directionLabel} flow(s) from {pcapPath}");
        foreach (var f in flows)
        {
            Console.WriteLine($"  {f.Flow.Src} → {f.Flow.Dst}: {f.Bytes.Length} bytes from {f.SegmentCount} segments");
        }

        if (flows.Count == 0)
        {
            Console.WriteLine("No S→C flows.");
            return;
        }

        

        var totalSuccess = 0;
        var totalFailed = 0;
        var totalFrames = 0;
        var totalLeftover = 0;
        var aggregateOpcodes = new Dictionary<ushort, int>();

        foreach (var flow in flows.OrderByDescending(f => f.Bytes.Length))
        {
            Console.WriteLine($"\n=== Flow {flow.Flow.Src} → {flow.Flow.Dst} ({flow.Bytes.Length} bytes) ===");
            DecryptOneFlow(flow.Bytes, table, derivation,
                ref totalSuccess, ref totalFailed, ref totalFrames, ref totalLeftover,
                aggregateOpcodes);
        }

        Console.WriteLine($"\n========================================");
        Console.WriteLine($"AGGREGATE: {totalSuccess} decoded ({totalFailed} failed) from {totalFrames} extracted frames");
        Console.WriteLine($"           {totalLeftover} bytes left undecoded across all flows");
        Console.WriteLine($"\nAggregate opcode distribution ({aggregateOpcodes.Count} distinct):");
        foreach (var (op, count) in aggregateOpcodes.OrderByDescending(kv => kv.Value).Take(30))
            Console.WriteLine($"  0x{op:X4} × {count}");
        return;
    }

    private static void DecryptOneFlow(
        byte[] streamBytes, KeyTable table, S2v2010Derivation derivation,
        ref int totalSuccess, ref int totalFailed, ref int totalFrames, ref int totalLeftover,
        Dictionary<ushort, int> aggregateOpcodes)
    {
        if (streamBytes.Length < S2PacketCipher.MinimumFrameSize)
        {
            Console.WriteLine("  too short, skipping");
            return;
        }

        try { derivation.ValidatePackingFlags(streamBytes.AsSpan(0, S2PacketCipher.MinimumFrameSize), table); }
        catch (InvalidPacketException ex)
        {
            Console.WriteLine($"  Magic check FAILED on first 9 bytes: {ex.Message}");
            return;
        }

        var (bestKey, bestFrameCount) = BruteForceInitialKey(table, derivation, streamBytes);
        Console.WriteLine($"  best initial key: 0x{bestKey:X2} → {bestFrameCount} drained frames");

        var cipher = new S2PacketCipher(table, derivation);
        var session = new SessionInfo(bestKey) { IsStrict = false };
        var framer = new S2StreamFramer(table, derivation, bestKey);
        framer.Feed(streamBytes);

        

        
        var success = 0;
        var failed = 0;
        var totalFramesExtracted = 0;
        var failureReasons = new Dictionary<string, int>();
        var keyUpdates = 0;
        var consumed = 0;
        while (framer.TryDrainOne(out var f))
        {
            totalFramesExtracted++;
            try
            {
                var decoded = cipher.Decrypt(f, session);
                framer.OnSessionKeyAdvanced(session.Key);
                success++;
                aggregateOpcodes[decoded.Opcode] = aggregateOpcodes.GetValueOrDefault(decoded.Opcode) + 1;
                if ((decoded.Flags & PackingFlags.KeyUpdate) != 0) keyUpdates++;
            }
            catch (InvalidPacketException ex)
            {
                failed++;
                var bucket = ex.Message.Split(':', 2)[0];
                failureReasons[bucket] = failureReasons.GetValueOrDefault(bucket) + 1;
            }
            consumed += f.Length;
        }
        var leftover = framer.Buffered;
        Console.WriteLine($"  frames extracted: {totalFramesExtracted} | leftover bytes: {leftover}");
        Console.WriteLine($"  pipeline: {success} decoded, {failed} failed, {keyUpdates} key-updates, final session key = 0x{session.Key:X2}");

        

        
        if (leftover > 0)
        {
            var desyncOff = consumed;
            var window = streamBytes.AsSpan(desyncOff, Math.Min(48, leftover));
            var hex = string.Join(' ', window.ToArray().Select(b => $"{b:X2}"));
            Console.WriteLine($"  desync at byte offset {desyncOff} (head 48 bytes:");
            Console.WriteLine($"    {hex}");

            if (desyncOff >= 16)
            {
                var prev = streamBytes.AsSpan(desyncOff - 16, 16);
                var phex = string.Join(' ', prev.ToArray().Select(b => $"{b:X2}"));
                Console.WriteLine($"    prev16: {phex}");
            }
        }

        if (failureReasons.Count > 0)
        {
            foreach (var (reason, count) in failureReasons.OrderByDescending(kv => kv.Value))
                Console.WriteLine($"    fail {count}× {reason}");
        }

        totalSuccess += success;
        totalFailed += failed;
        totalFrames += totalFramesExtracted;
        totalLeftover += leftover;
    }

    private static (byte Key, int FrameCount) BruteForceInitialKey(
        KeyTable table, S2v2010Derivation derivation, byte[] streamBytes)
    {
        byte bestKey = 0;
        var bestFrames = -1;
        for (var k = 0; k < 256; k++)
        {
            var framer = new S2StreamFramer(table, derivation, (byte)k);
            framer.Feed(streamBytes);
            var frames = framer.DrainFrames();
            if (frames.Count > bestFrames)
            {
                bestFrames = frames.Count;
                bestKey = (byte)k;
            }
        }
        return (bestKey, bestFrames);
    }
}
