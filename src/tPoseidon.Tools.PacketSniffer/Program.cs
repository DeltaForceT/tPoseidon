using System.Net;
using System.Net.Sockets;
using System.Text;
using tPoseidon.Tools.PacketSniffer;
using PacketDotNet;
using SharpPcap;
using SharpPcap.LibPcap;

static int ParsePortFlag(string[] args, int defaultPort)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--port" && int.TryParse(args[i + 1], out var p) && p is > 0 and <= 65535)
        {
            return p;
        }
    }
    return defaultPort;
}

static bool ParseClientToServerFlag(string[] args) => args.Contains("--c2s");

if (args.Length >= 1 && args[0] == "decrypt")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: tPoseidon.Tools.PacketSniffer decrypt <keytable.bin> <pcap> [--port N] [--c2s]");
        return 1;
    }
    DecryptCapture.Run(args[1], args[2], ParsePortFlag(args, defaultPort: 22006), ParseClientToServerFlag(args));
    return 0;
}

if (args.Length >= 1 && args[0] == "raw")
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: tPoseidon.Tools.PacketSniffer raw <pcap> [--port N] [--c2s] [--max N]");
        return 1;
    }
    var maxBytes = 4096;
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--max" && int.TryParse(args[i + 1], out var m) && m > 0)
        {
            maxBytes = m;
        }
    }
    InspectRaw.Run(args[1], ParsePortFlag(args, defaultPort: 22006), ParseClientToServerFlag(args), maxBytes);
    return 0;
}

if (args.Length >= 1 && args[0] == "decrypt-frame")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: tPoseidon.Tools.PacketSniffer decrypt-frame <keytable.bin> <frame.bin>");
        return 1;
    }
    return DecryptFrame.Run(args[1], args[2]);
}

if (args.Length >= 1 && args[0] == "extract-flow")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: tPoseidon.Tools.PacketSniffer extract-flow <pcap> <output.bin> [--port N] [--c2s]");
        return 1;
    }
    var pcapBytes = File.ReadAllBytes(args[1]);
    var port = ParsePortFlag(args, defaultPort: 22006);
    var c2s = ParseClientToServerFlag(args);
    var flows = c2s
        ? PcapTcpReassembler.ReassembleClientToServer(pcapBytes, port)
        : PcapTcpReassembler.ReassembleServerToClient(pcapBytes, port);
    if (flows.Count == 0)
    {
        Console.Error.WriteLine($"No {(c2s ? "C->S" : "S->C")} flows on port {port}.");
        return 2;
    }
    var biggest = flows.OrderByDescending(f => f.Bytes.Length).First();
    File.WriteAllBytes(args[2], biggest.Bytes);
    Console.WriteLine($"Wrote {biggest.Bytes.Length} bytes ({biggest.SegmentCount} segments, flow {biggest.Flow.Src} -> {biggest.Flow.Dst}) to {args[2]}");
    return 0;
}

if (args.Length >= 1 && args[0] == "chrono")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: tPoseidon.Tools.PacketSniffer chrono <keytable.bin> <pcap> [--port N]");
        return 1;
    }
    InspectChrono.Run(args[1], args[2], ParsePortFlag(args, defaultPort: 22006));
    return 0;
}

if (args.Length >= 1 && args[0] == "inspect")
{
    if (args.Length < 3)
    {
        Console.Error.WriteLine("Usage: tPoseidon.Tools.PacketSniffer inspect <keytable.bin> <pcap> [opcode] [--port N] [--c2s]");
        return 1;
    }
    ushort? filterOpcode = null;

    if (args.Length >= 4 && !args[3].StartsWith("--", StringComparison.Ordinal))
    {
        var s = args[3];
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (!ushort.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var op))
        {
            Console.Error.WriteLine($"Bad opcode: {args[3]} (expected hex like 0x0008)");
            return 1;
        }
        filterOpcode = op;
    }
    var ordered = args.Contains("--ordered");
    InspectOpcodes.Run(args[1], args[2], filterOpcode, ParsePortFlag(args, defaultPort: 22006), ParseClientToServerFlag(args), ordered);
    return 0;
}

if (args.Length < 2)
{
    Console.Error.WriteLine(
        "Usage:\n" +
        "  tPoseidon.Tools.PacketSniffer <host> <port> [--outfile path]\n" +
        "  tPoseidon.Tools.PacketSniffer decrypt <keytable.bin> <pcap> [--port N] [--c2s]\n" +
        "  tPoseidon.Tools.PacketSniffer decrypt-frame <keytable.bin> <frame.bin>\n" +
        "  tPoseidon.Tools.PacketSniffer inspect <keytable.bin> <pcap> [opcode] [--port N] [--c2s]\n" +
        "  tPoseidon.Tools.PacketSniffer raw <pcap> [--port N] [--c2s] [--max N]");
    return 1;
}

var remoteHost = args[0];

if (!int.TryParse(args[1], out var remotePort) || remotePort is < 0 or > 65535)
{
    Console.Error.WriteLine($"Bad port: {args[1]} (use 0 for 'all ports')");
    return 1;
}

string? outFile = null;
int? deviceIndex = null;
for (var i = 2; i < args.Length - 1; i++)
{
    if (args[i] == "--outfile") outFile = args[i + 1];
    else if (args[i] == "--device" && int.TryParse(args[i + 1], out var idx)) deviceIndex = idx;
}

IPAddress? targetIp = IPAddress.TryParse(remoteHost, out var parsed) ? parsed : null;
if (targetIp is null)
{
    try
    {
        var entry = await Dns.GetHostEntryAsync(remoteHost);
        targetIp = entry.AddressList.FirstOrDefault(
            a => a.AddressFamily == AddressFamily.InterNetwork);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"DNS resolution for '{remoteHost}' failed: {ex.Message}");
        return 1;
    }
}

if (targetIp is null)
{
    Console.Error.WriteLine($"No IPv4 address for {remoteHost}.");
    return 1;
}

Console.WriteLine($"Target: {targetIp}:{remotePort}");

var devices = CaptureDeviceList.Instance;
if (devices.Count == 0)
{
    Console.Error.WriteLine("No capture devices found. Install Npcap: https://npcap.com");
    return 2;
}

Console.WriteLine("\nAvailable capture devices:");
for (var i = 0; i < devices.Count; i++)
{
    Console.WriteLine($"  [{i}] {devices[i].Description}");
}

int index;
if (deviceIndex is not null)
{
    index = deviceIndex.Value;
    Console.WriteLine($"\nUsing device --device {index} (non-interactive).");
}
else
{
    Console.Write("\nSelect device index (default 0): ");
    var line = Console.ReadLine();
    index = string.IsNullOrWhiteSpace(line) ? 0 : int.Parse(line);
}
var device = devices[index];

device.Open(DeviceModes.Promiscuous, 1000);

var noHostFilter = targetIp.Equals(IPAddress.Any) || remoteHost == "*";
device.Filter = noHostFilter
    ? (remotePort == 0 ? "tcp" : $"tcp and port {remotePort}")
    : (remotePort == 0
        ? $"tcp and host {targetIp}"
        : $"tcp and host {targetIp} and port {remotePort}");
Console.WriteLine($"\nFilter: {device.Filter}");

CaptureFileWriterDevice? writer = null;
if (outFile is not null)
{
    writer = new CaptureFileWriterDevice(outFile);
    writer.Open(device);
    Console.WriteLine($"Writing pcap to: {outFile}");
}

Console.WriteLine("\nCapturing. Press Ctrl+C to stop.\n");

var packetCount = 0;
device.OnPacketArrival += (_, e) =>
{
    var packet = Packet.ParsePacket(e.Device.LinkType, e.Data.ToArray());
    var tcp = packet.Extract<TcpPacket>();
    if (tcp is null) return;

    packetCount++;
    var ipPkt = packet.Extract<PacketDotNet.IPv4Packet>();
    var srcIp = ipPkt?.SourceAddress?.ToString() ?? "?";
    var dstIp = ipPkt?.DestinationAddress?.ToString() ?? "?";
    var direction = remotePort == 0

        
        ? (dstIp == targetIp.ToString() ? "C->S" : "S->C")
        : (tcp.DestinationPort == remotePort ? "C->S" : "S->C");
    var len = tcp.PayloadData?.Length ?? 0;
    Console.WriteLine($"[{packetCount:D5}] {DateTime.UtcNow:HH:mm:ss.fff} {direction} {srcIp}:{tcp.SourcePort} -> {dstIp}:{tcp.DestinationPort} seq={tcp.SequenceNumber} ack={tcp.AcknowledgmentNumber} len={len}");
    if (len > 0) WriteHex(tcp.PayloadData!);

    writer?.Write(e.GetPacket());
};

device.StartCapture();

var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
try { await Task.Delay(Timeout.Infinite, stop.Token); }
catch (OperationCanceledException) { }

device.StopCapture();
device.Close();
writer?.Close();

Console.WriteLine($"\nDone. Captured {packetCount} TCP segments.");
return 0;

static void WriteHex(byte[] data)
{
    var sb = new StringBuilder();
    for (var i = 0; i < data.Length; i += 16)
    {
        sb.Append($"    {i:X4}  ");
        for (var j = 0; j < 16; j++)
        {
            sb.Append(i + j < data.Length ? $"{data[i + j]:X2} " : "   ");
            if (j == 7) sb.Append(' ');
        }
        sb.Append("  ");
        for (var j = 0; j < 16 && i + j < data.Length; j++)
        {
            var c = data[i + j];
            sb.Append(c is >= 0x20 and <= 0x7e ? (char)c : '.');
        }
        sb.AppendLine();
    }
    Console.Write(sb.ToString());
}
