using System.Buffers.Binary;
using System.Net;

namespace tPoseidon.Tools.PacketSniffer;

internal static class PcapTcpReassembler
{
    public readonly record struct Flow(IPEndPoint Src, IPEndPoint Dst);

    public sealed record FlowStream(Flow Flow, byte[] Bytes, int SegmentCount);

    public sealed record FlowStreamWithTimings(Flow Flow, byte[] Bytes, int SegmentCount, IReadOnlyList<long> ByteOffsetToUs);

    public enum Direction { ServerToClient, ClientToServer }

    public static List<FlowStream> ReassembleClientToServer(byte[] pcap, int serverPort) =>
        ReassembleByDirection(pcap, serverPort, Direction.ClientToServer);

    public static List<FlowStream> ReassembleServerToClient(byte[] pcap, int serverPort) =>
        ReassembleByDirection(pcap, serverPort, Direction.ServerToClient);

    public static List<FlowStreamWithTimings> ReassembleServerToClientWithTimings(byte[] pcap, int serverPort) =>
        ReassembleByDirectionWithTimings(pcap, serverPort, Direction.ServerToClient);

    public static List<FlowStreamWithTimings> ReassembleClientToServerWithTimings(byte[] pcap, int serverPort) =>
        ReassembleByDirectionWithTimings(pcap, serverPort, Direction.ClientToServer);

    private static List<FlowStream> ReassembleByDirection(byte[] pcap, int serverPort, Direction dir)
    {
        var perFlow = new Dictionary<Flow, List<(uint Seq, byte[] Payload)>>();

        if (pcap.Length < 24) return new List<FlowStream>();

        var linkType = (int)BinaryPrimitives.ReadUInt32LittleEndian(pcap.AsSpan(20, 4));
        var linkHeaderBytes = linkType switch
        {
            1 => 14,
            0 => 4,
            _ => 4,
        };

        var offset = 24;
        while (offset + 16 <= pcap.Length)
        {
            var inclLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(pcap.AsSpan(offset + 8, 4));
            offset += 16;
            if (offset + inclLen > pcap.Length) break;
            var pkt = pcap.AsSpan(offset, inclLen);
            offset += inclLen;

            if (pkt.Length < linkHeaderBytes + 20) continue;

            if (linkType == 1)
            {
                var ethertype = BinaryPrimitives.ReadUInt16BigEndian(pkt.Slice(12, 2));
                if (ethertype != 0x0800) continue;
            }

            var ip = pkt.Slice(linkHeaderBytes);

            if (ip.Length < 20) continue;
            if ((ip[0] >> 4) != 4) continue;
            if (ip[9] != 6) continue;
            var srcIp = new IPAddress(ip.Slice(12, 4).ToArray());
            var dstIp = new IPAddress(ip.Slice(16, 4).ToArray());
            var ihl = (ip[0] & 0x0F) * 4;
            var tcp = ip.Slice(ihl);
            if (tcp.Length < 20) continue;
            var srcPort = BinaryPrimitives.ReadUInt16BigEndian(tcp.Slice(0, 2));
            var dstPort = BinaryPrimitives.ReadUInt16BigEndian(tcp.Slice(2, 2));

            if (dir == Direction.ServerToClient && srcPort != serverPort) continue;
            if (dir == Direction.ClientToServer && dstPort != serverPort) continue;

            var seq = BinaryPrimitives.ReadUInt32BigEndian(tcp.Slice(4, 4));
            var dataOff = (tcp[12] >> 4) * 4;
            if (dataOff < 20 || dataOff > tcp.Length) continue;
            var payload = tcp.Slice(dataOff);
            if (payload.Length == 0) continue;

            var flow = new Flow(new IPEndPoint(srcIp, srcPort), new IPEndPoint(dstIp, dstPort));
            if (!perFlow.TryGetValue(flow, out var segs))
            {
                segs = new List<(uint, byte[])>();
                perFlow[flow] = segs;
            }
            segs.Add((seq, payload.ToArray()));
        }

        var result = new List<FlowStream>(perFlow.Count);
        foreach (var (flow, segs) in perFlow)
        {
            var origin = segs[0].Seq;
            segs.Sort((a, b) => ((int)(a.Seq - origin)).CompareTo((int)(b.Seq - origin)));

            using var ms = new MemoryStream();
            var expected = segs[0].Seq;
            var emitted = 0;
            foreach (var (seq, payload) in segs)
            {
                var delta = (int)(seq - expected);
                if (delta < 0)
                {
                    var skip = -delta;
                    if (skip >= payload.Length) continue;
                    ms.Write(payload, skip, payload.Length - skip);
                    expected = seq + (uint)payload.Length;
                    emitted++;
                    continue;
                }
                if (delta > 0)
                {
                    break;
                }
                ms.Write(payload, 0, payload.Length);
                expected = seq + (uint)payload.Length;
                emitted++;
            }

            result.Add(new FlowStream(flow, ms.ToArray(), emitted));
        }

        return result;
    }

    private static List<FlowStreamWithTimings> ReassembleByDirectionWithTimings(byte[] pcap, int serverPort, Direction dir)
    {
        var perFlow = new Dictionary<Flow, List<(uint Seq, byte[] Payload, long TsUs)>>();

        if (pcap.Length < 24) return new List<FlowStreamWithTimings>();

        var linkType = (int)BinaryPrimitives.ReadUInt32LittleEndian(pcap.AsSpan(20, 4));
        var linkHeaderBytes = linkType switch { 1 => 14, 0 => 4, _ => 4 };

        long firstTsUs = -1;
        var offset = 24;
        while (offset + 16 <= pcap.Length)
        {
            var tsSec = (long)BinaryPrimitives.ReadUInt32LittleEndian(pcap.AsSpan(offset + 0, 4));
            var tsUsec = (long)BinaryPrimitives.ReadUInt32LittleEndian(pcap.AsSpan(offset + 4, 4));
            var inclLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(pcap.AsSpan(offset + 8, 4));
            offset += 16;
            if (offset + inclLen > pcap.Length) break;
            var pkt = pcap.AsSpan(offset, inclLen);
            offset += inclLen;

            if (pkt.Length < linkHeaderBytes + 20) continue;
            if (linkType == 1)
            {
                var ethertype = BinaryPrimitives.ReadUInt16BigEndian(pkt.Slice(12, 2));
                if (ethertype != 0x0800) continue;
            }
            var ip = pkt.Slice(linkHeaderBytes);
            if (ip.Length < 20) continue;
            if ((ip[0] >> 4) != 4) continue;
            if (ip[9] != 6) continue;
            var srcIp = new IPAddress(ip.Slice(12, 4).ToArray());
            var dstIp = new IPAddress(ip.Slice(16, 4).ToArray());
            var ihl = (ip[0] & 0x0F) * 4;
            var tcp = ip.Slice(ihl);
            if (tcp.Length < 20) continue;
            var srcPort = BinaryPrimitives.ReadUInt16BigEndian(tcp.Slice(0, 2));
            var dstPort = BinaryPrimitives.ReadUInt16BigEndian(tcp.Slice(2, 2));
            if (dir == Direction.ServerToClient && srcPort != serverPort) continue;
            if (dir == Direction.ClientToServer && dstPort != serverPort) continue;
            var seq = BinaryPrimitives.ReadUInt32BigEndian(tcp.Slice(4, 4));
            var dataOff = (tcp[12] >> 4) * 4;
            if (dataOff < 20 || dataOff > tcp.Length) continue;
            var payload = tcp.Slice(dataOff);
            if (payload.Length == 0) continue;
            var pktTsUs = tsSec * 1_000_000L + tsUsec;
            if (firstTsUs < 0) firstTsUs = pktTsUs;
            var flow = new Flow(new IPEndPoint(srcIp, srcPort), new IPEndPoint(dstIp, dstPort));
            if (!perFlow.TryGetValue(flow, out var segs))
            {
                segs = new List<(uint, byte[], long)>();
                perFlow[flow] = segs;
            }
            segs.Add((seq, payload.ToArray(), pktTsUs - firstTsUs));
        }

        var result = new List<FlowStreamWithTimings>(perFlow.Count);
        foreach (var (flow, segs) in perFlow)
        {
            var origin = segs[0].Seq;
            segs.Sort((a, b) => ((int)(a.Seq - origin)).CompareTo((int)(b.Seq - origin)));
            using var ms = new MemoryStream();
            var byteToTs = new List<long>();
            var expected = segs[0].Seq;
            var emitted = 0;
            foreach (var (seq, payload, tsUs) in segs)
            {
                var delta = (int)(seq - expected);
                int writeStart;
                int writeLen;
                if (delta < 0)
                {
                    var skip = -delta;
                    if (skip >= payload.Length) continue;
                    writeStart = skip;
                    writeLen = payload.Length - skip;
                }
                else if (delta > 0)
                {
                    break;
                }
                else
                {
                    writeStart = 0;
                    writeLen = payload.Length;
                }
                ms.Write(payload, writeStart, writeLen);
                for (int i = 0; i < writeLen; i++) byteToTs.Add(tsUs);
                expected = seq + (uint)payload.Length;
                emitted++;
            }
            result.Add(new FlowStreamWithTimings(flow, ms.ToArray(), emitted, byteToTs));
        }

        return result;
    }
}
