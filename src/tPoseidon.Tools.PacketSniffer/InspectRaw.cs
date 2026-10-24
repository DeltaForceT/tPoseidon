namespace tPoseidon.Tools.PacketSniffer;

internal static class InspectRaw
{
    public static void Run(string pcapPath, int serverPort, bool clientToServer = false, int maxBytes = 4096)
    {
        var pcap = File.ReadAllBytes(pcapPath);
        var directionLabel = clientToServer ? "C→S" : "S→C";
        var flows = clientToServer
            ? PcapTcpReassembler.ReassembleClientToServer(pcap, serverPort)
            : PcapTcpReassembler.ReassembleServerToClient(pcap, serverPort);

        Console.WriteLine($"Reassembled {flows.Count} {directionLabel} flow(s) from {pcapPath} (port {serverPort})");

        if (flows.Count == 0)
        {
            Console.WriteLine("(no flows on that port)");
            return;
        }

        foreach (var flow in flows)
        {
            Console.WriteLine();
            Console.WriteLine($"=== {flow.Flow.Src} → {flow.Flow.Dst}: {flow.Bytes.Length} bytes from {flow.SegmentCount} segments ===");
            DumpHex(flow.Bytes, maxBytes);
        }
    }

    private static void DumpHex(byte[] bytes, int maxBytes)
    {
        var n = Math.Min(bytes.Length, maxBytes);
        for (var off = 0; off < n; off += 16)
        {
            var line = new System.Text.StringBuilder();
            line.Append($"  [{off:X4}]  ");
            for (var i = 0; i < 16; i++)
            {
                line.Append(off + i < n ? $"{bytes[off + i]:X2} " : "   ");
                if (i == 7) line.Append(' ');
            }
            line.Append("  ");
            for (var i = 0; i < 16 && off + i < n; i++)
            {
                var b = bytes[off + i];
                line.Append(b is >= 0x20 and <= 0x7E ? (char)b : '.');
            }
            Console.WriteLine(line.ToString());
        }
        if (bytes.Length > maxBytes)
        {
            Console.WriteLine($"  ... {bytes.Length - maxBytes} more bytes (truncated; pass --max <N> for more)");
        }
    }
}
