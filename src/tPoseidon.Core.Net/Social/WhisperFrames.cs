using System.Text;

namespace tPoseidon.Core.Net.Social;

public readonly record struct WhisperRequest(ushort RoomId, bool ByName, ushort TargetClientId, string TargetName, string Text);

public static class WhisperFrames
{
    public const ushort EnterLobby = 0x1771;
    public const ushort LobbyEntered = 0x1773;
    public const ushort Whisper = 0x1777;
    public const ushort WhisperTargetMissing = 0x1778;
    public const ushort WhisperSent = 0x1779;
    public const ushort WhisperReceived = 0x1793;

    public static byte[] Entered(ushort clientId, ushort roomId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(clientId);
        w.WriteUInt16(roomId);
        return w.ToArray();
    }

    public static bool TryRead(ReadOnlySpan<byte> body, Encoding text, out WhisperRequest request)
    {
        ArgumentNullException.ThrowIfNull(text);
        request = default;
        if (body.Length < 3) return false;
        var r = new PacketReader(body);
        var roomId = r.ReadUInt16();
        var byName = r.ReadByte() != 0;
        if (byName)
        {
            var name = r.ReadNulTerminated(text);
            request = new WhisperRequest(roomId, true, 0, name, r.ReadNulTerminated(text));
        }
        else
        {
            if (body.Length < 5) return false;
            var target = r.ReadUInt16();
            request = new WhisperRequest(roomId, false, target, string.Empty, r.ReadNulTerminated(text));
        }
        return true;
    }

    public static byte[] ToSenderByName(ushort roomId, string targetName, Encoding text)
    {
        var w = new PacketWriter(24);
        w.WriteUInt16(roomId);
        w.WriteByte(1);
        w.WriteNulTerminated(targetName, text);
        return w.ToArray();
    }

    public static byte[] ToSenderById(ushort roomId, ushort targetClientId)
    {
        var w = new PacketWriter(5);
        w.WriteUInt16(roomId);
        w.WriteByte(0);
        w.WriteUInt16(targetClientId);
        return w.ToArray();
    }

    public static byte[] ToTargetByName(ushort roomId, string senderName, string message, Encoding text)
    {
        var w = new PacketWriter(64);
        w.WriteUInt16(roomId);
        w.WriteByte(1);
        w.WriteNulTerminated(senderName, text);
        w.WriteNulTerminated(message, text);
        return w.ToArray();
    }

    public static byte[] ToTargetById(ushort roomId, ushort senderClientId, string message, Encoding text)
    {
        var w = new PacketWriter(64);
        w.WriteUInt16(roomId);
        w.WriteByte(0);
        w.WriteUInt16(senderClientId);
        w.WriteNulTerminated(message, text);
        return w.ToArray();
    }
}
