using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Tools.PacketSniffer;

internal static class InspectChrono
{
    private readonly record struct TimedFrame(long TsUs, char Dir, ushort Opcode, int Len, byte[] Head);

    public static void Run(string keyTablePath, string pcapPath, int serverPort = 22006)
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

        var s2c = PcapTcpReassembler.ReassembleServerToClientWithTimings(pcap, serverPort);
        var c2s = PcapTcpReassembler.ReassembleClientToServerWithTimings(pcap, serverPort);

        var collected = new List<TimedFrame>();
        var sCount = DecodeAll(table, derivation, s2c, 'S', collected);
        var cCount = DecodeAll(table, derivation, c2s, 'C', collected);
        Console.Error.WriteLine($"[chrono] {s2c.Count} S2C flows ({sCount} frames), {c2s.Count} C2S flows ({cCount} frames)");

        collected.Sort((a, b) => a.TsUs.CompareTo(b.TsUs));

        long firstUs = collected.Count > 0 ? collected[0].TsUs : 0;
        long lastUs = -1;
        var frameIdx = 0;
        foreach (var tf in collected)
        {
            long relUs = tf.TsUs - firstUs;
            long deltaUs = lastUs < 0 ? 0 : (tf.TsUs - lastUs);
            lastUs = tf.TsUs;
            var head = tf.Head.Length == 0
                ? "<empty>"
                : Convert.ToHexString(tf.Head.AsSpan(0, Math.Min(16, tf.Head.Length)));
            Console.WriteLine($"f{frameIdx,4}  t=+{relUs / 1000.0,9:F3}ms  d=+{deltaUs / 1000.0,7:F3}ms  {tf.Dir}  op=0x{tf.Opcode:X4}  len={tf.Len,4}B  {head}");
            frameIdx++;
        }
    }

    private static int DecodeAll(
        KeyTable table,
        S2v2010Derivation derivation,
        List<PcapTcpReassembler.FlowStreamWithTimings> flows,
        char dir,
        List<TimedFrame> collected)
    {
        var produced = 0;
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

            var byteCursor = 0;
            while (framer.TryDrainOne(out var f))
            {
                S2DecryptedPacket dec;
                int frameLen = f.Length;
                try { dec = cipher.Decrypt(f, session); }
                catch (InvalidPacketException) { byteCursor += frameLen; continue; }
                framer.OnSessionKeyAdvanced(session.Key);

                long tsUs = byteCursor < flow.ByteOffsetToUs.Count
                    ? flow.ByteOffsetToUs[byteCursor]
                    : (flow.ByteOffsetToUs.Count > 0 ? flow.ByteOffsetToUs[^1] : 0);

                var head = dec.Payload.AsSpan(0, Math.Min(16, dec.Payload.Length)).ToArray();
                collected.Add(new TimedFrame(tsUs, dir, dec.Opcode, dec.Payload.Length, head));

                byteCursor += frameLen;
                produced++;
            }
        }
        return produced;
    }

    private static (byte Key, int Frames) BruteForceInitialKey(
        KeyTable table, S2v2010Derivation derivation, byte[] streamBytes)
    {
        byte best = 0;
        var bestN = -1;
        for (var k = 0; k < 256; k++)
        {
            var framer = new S2StreamFramer(table, derivation, (byte)k);
            framer.Feed(streamBytes);
            var cipher = new S2PacketCipher(table, derivation);
            var session = new SessionInfo((byte)k) { IsStrict = false };
            var decrypted = 0;
            while (framer.TryDrainOne(out var f))
            {
                try
                {
                    cipher.Decrypt(f, session);
                    framer.OnSessionKeyAdvanced(session.Key);
                    decrypted++;
                }
                catch (InvalidPacketException)
                {
                    break;
                }
            }
            if (decrypted > bestN) { bestN = decrypted; best = (byte)k; }
        }
        return (best, bestN);
    }
}
