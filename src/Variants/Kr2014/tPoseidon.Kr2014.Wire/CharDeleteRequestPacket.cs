using tPoseidon.Core.Common;
using tPoseidon.Core.Net;

namespace tPoseidon.Kr2014.Wire;

public sealed class CharDeleteRequestPacket
{
    public const int SecondPasswordDigestFieldBytes = 17;
    public const int PayloadSize = sizeof(ulong) + SecondPasswordDigestFieldBytes;

    private const char FieldTerminator = (char)0;

    public required ulong CharacterUid { get; init; }

    public string SecondPasswordDigest { get; init; } = string.Empty;

    public byte[] EncodePayload()
    {
        ArgumentNullException.ThrowIfNull(SecondPasswordDigest);

        var w = new PacketWriter(PayloadSize);
        w.WriteUInt64(CharacterUid);
        w.WriteFixedString(SecondPasswordDigest, SecondPasswordDigestFieldBytes);
        return w.ToArray();
    }

    public static CharDeleteRequestPacket DecodePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length != PayloadSize)
        {
            throw new InvalidPacketException(
                $"Kr2014 CharDelete payload must be {PayloadSize} bytes, got {payload.Length}.");
        }
        var r = new PacketReader(payload);
        var characterUid = r.ReadUInt64();
        var digest = r.ReadFixedString(SecondPasswordDigestFieldBytes);
        var terminator = digest.IndexOf(FieldTerminator);
        if (terminator >= 0)
        {
            digest = digest[..terminator];
        }
        return new CharDeleteRequestPacket
        {
            CharacterUid = characterUid,
            SecondPasswordDigest = digest,
        };
    }
}
