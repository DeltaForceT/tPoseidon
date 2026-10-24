

using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Tools.PacketSniffer;

internal static class DecryptFrame
{
    public static int Run(string keyTablePath, string framePath)
    {
        if (!File.Exists(keyTablePath))
        {
            Console.Error.WriteLine($"KeyTable not found: {keyTablePath}");
            return 1;
        }
        if (!File.Exists(framePath))
        {
            Console.Error.WriteLine($"Frame not found: {framePath}");
            return 1;
        }

        var keyBytes = File.ReadAllBytes(keyTablePath);
        if (keyBytes.Length != KeyTable.RequiredLength)
        {
            Console.Error.WriteLine($"KeyTable wrong size: {keyBytes.Length}, want {KeyTable.RequiredLength}");
            return 1;
        }

        var table = new KeyTable(keyBytes);
        var derivation = new S2v2010Derivation();
        var cipher = new S2PacketCipher(table, derivation);

        var bytes = File.ReadAllBytes(framePath);
        Console.WriteLine($"Loaded {bytes.Length}-byte file from {framePath}");

        

        
        SessionInfo? session = null;
        byte initialKey = 0;
        S2DecryptedPacket? firstFrame = null;
        for (var k = 0; k <= 0xFF; k++)
        {
            var trial = new SessionInfo((byte)k) { IsStrict = false };
            try
            {
                var decoded = cipher.Decrypt(bytes, trial);
                session = trial;
                initialKey = (byte)k;
                firstFrame = decoded;
                break;
            }
            catch (InvalidPacketException)
            {

            }
        }

        if (session is null || firstFrame is null)
        {
            Console.Error.WriteLine("FAILED: no session key in 0..255 produced a valid decrypt of the first frame.");
            return 2;
        }

        Console.WriteLine();
        Console.WriteLine($"=== INITIAL KEY: 0x{initialKey:X2} ===");

        

        
        session = new SessionInfo(initialKey) { IsStrict = false };
        var frames = new List<S2DecryptedPacket>();
        var offset = 0;
        var frameIndex = 0;
        while (offset < bytes.Length)
        {

            

            

            

            var remaining = new ReadOnlySpan<byte>(bytes, offset, bytes.Length - offset);
            S2DecryptedPacket pkt;
            try
            {
                pkt = cipher.Decrypt(remaining, session);
            }
            catch (InvalidPacketException ex)
            {
                Console.Error.WriteLine($"FAILED at frame {frameIndex} (offset 0x{offset:X4}): {ex.Message}");
                return 3;
            }

            frames.Add(pkt);
            Console.WriteLine($"  [{frameIndex}] offset=0x{offset:X4} len={pkt.DeclaredLength} opcode=0x{pkt.Opcode:X4} seq={pkt.SequenceNumber} flags={pkt.Flags} randKey=0x{pkt.RandKey:X2} payload={pkt.Payload.Length}B");

            offset += pkt.DeclaredLength;
            frameIndex++;
        }

        Console.WriteLine();
        Console.WriteLine($"=== DECODED {frames.Count} FRAME(S) ===");

        

        if (frames.Count == 1)
        {
            WriteSingleFrameOutputs(framePath, frames[0], initialKey);
        }
        else
        {
            WriteMultiFrameOutputs(framePath, frames, initialKey);
        }

        return 0;
    }

    private static void WriteSingleFrameOutputs(string framePath, S2DecryptedPacket pkt, byte initialKey)
    {
        var plainPath = Path.ChangeExtension(framePath, ".plain.bin");
        File.WriteAllBytes(plainPath, pkt.Payload);
        Console.WriteLine($"  wrote plaintext payload : {plainPath} ({pkt.Payload.Length} bytes)");

        var hexPath = Path.ChangeExtension(framePath, ".hex.txt");
        var sb = new StringBuilder();
        sb.AppendLine($"# {Path.GetFileName(framePath)} — decrypted plaintext payload");
        sb.AppendLine($"# opcode=0x{pkt.Opcode:X4} sequence={pkt.SequenceNumber} flags={pkt.Flags}");
        sb.AppendLine($"# initialSessionKey=0x{initialKey:X2} randKey=0x{pkt.RandKey:X2}");
        sb.AppendLine($"# payload length: {pkt.Payload.Length} bytes");
        sb.AppendLine();
        AppendHexDump(sb, pkt.Payload);
        File.WriteAllText(hexPath, sb.ToString());
        Console.WriteLine($"  wrote hex dump          : {hexPath}");
    }

    private static void WriteMultiFrameOutputs(string framePath, List<S2DecryptedPacket> frames, byte initialKey)
    {
        var dir = Path.GetDirectoryName(framePath) ?? ".";
        var baseName = Path.GetFileNameWithoutExtension(framePath);

        var streamPath = Path.Combine(dir, baseName + ".stream.txt");
        var stream = new StringBuilder();
        stream.AppendLine($"# {Path.GetFileName(framePath)} — multi-frame decrypted stream");
        stream.AppendLine($"# initialSessionKey=0x{initialKey:X2}");
        stream.AppendLine($"# total frames: {frames.Count}");
        stream.AppendLine();

        for (var i = 0; i < frames.Count; i++)
        {
            var pkt = frames[i];
            stream.AppendLine($"=== Frame {i}: opcode=0x{pkt.Opcode:X4} seq={pkt.SequenceNumber} flags={pkt.Flags} randKey=0x{pkt.RandKey:X2} payload={pkt.Payload.Length}B ===");
            AppendHexDump(stream, pkt.Payload);
            stream.AppendLine();

            var perFramePath = Path.Combine(dir, $"{baseName}.frame{i:D2}-op{pkt.Opcode:X4}.plain.bin");
            File.WriteAllBytes(perFramePath, pkt.Payload);
        }
        File.WriteAllText(streamPath, stream.ToString());
        Console.WriteLine($"  wrote stream summary    : {streamPath}");
        Console.WriteLine($"  wrote {frames.Count} per-frame .plain.bin sibling(s) under {dir}");
    }

    private static void AppendHexDump(StringBuilder sb, byte[] data)
    {
        for (var i = 0; i < data.Length; i += 16)
        {
            sb.Append($"{i:X4}  ");
            for (var j = 0; j < 16; j++)
            {
                if (i + j < data.Length) sb.Append($"{data[i + j]:X2} ");
                else sb.Append("   ");
                if (j == 7) sb.Append(' ');
            }
            sb.Append(" |");
            for (var j = 0; j < 16 && i + j < data.Length; j++)
            {
                var c = data[i + j];
                sb.Append(c is >= 0x20 and <= 0x7e ? (char)c : '.');
            }
            sb.AppendLine("|");
        }
    }
}
