using System.Buffers.Binary;

namespace tPoseidon.Core.Net.Dialogs;

public static class TunnelledRequest
{
    public const byte EmptyPassword = 0;

    public static byte[] Wrap(ushort innerOpcode, ReadOnlySpan<byte> body)
    {
        var payload = new byte[sizeof(ushort) + 1 + body.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, innerOpcode);
        payload[sizeof(ushort)] = EmptyPassword;
        body.CopyTo(payload.AsSpan(sizeof(ushort) + 1));
        return payload;
    }

    public static bool TrySplit(ReadOnlySpan<byte> payload, out ushort innerOpcode, out int bodyOffset)
    {
        innerOpcode = 0;
        bodyOffset = 0;
        if (payload.Length < sizeof(ushort)) return false;
        var passwordEnd = payload[sizeof(ushort)..].IndexOf((byte)0);
        if (passwordEnd < 0) return false;
        innerOpcode = BinaryPrimitives.ReadUInt16LittleEndian(payload);
        bodyOffset = sizeof(ushort) + passwordEnd + 1;
        return true;
    }
}
