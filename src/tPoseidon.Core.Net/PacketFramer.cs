using System.Buffers;
using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Net;

public sealed class PacketFramer
{
    private readonly ArrayBufferWriter<byte> _buffer = new();
    private readonly int _maxPacketBytes;

    public PacketFramer(int maxPacketBytes = 65535)
    {
        if (maxPacketBytes < MinimumPacketBytes || maxPacketBytes > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPacketBytes));
        }

        _maxPacketBytes = maxPacketBytes;
    }

    public const int MinimumPacketBytes = 2;

    public int BufferedBytes => _buffer.WrittenCount;

        public IReadOnlyList<byte[]> Feed(ReadOnlySpan<byte> incoming)
    {
        if (!incoming.IsEmpty)
        {
            _buffer.Write(incoming);
        }

        return DrainFrames();
    }

    private IReadOnlyList<byte[]> DrainFrames()
    {
        List<byte[]>? frames = null;

        while (true)
        {
            var written = _buffer.WrittenSpan;
            if (written.Length < MinimumPacketBytes)
            {
                break;
            }

            var declaredLength = BinaryPrimitives.ReadUInt16LittleEndian(written);

            if (declaredLength < MinimumPacketBytes)
            {
                throw new InvalidPacketException(
                    $"Declared packet length {declaredLength} is below minimum {MinimumPacketBytes}.");
            }

            if (declaredLength > _maxPacketBytes)
            {
                throw new InvalidPacketException(
                    $"Declared packet length {declaredLength} exceeds maximum {_maxPacketBytes}.");
            }

            if (written.Length < declaredLength)
            {
                break;
            }

            var frame = written[..declaredLength].ToArray();
            (frames ??= []).Add(frame);

            var remaining = written[declaredLength..].ToArray();
            _buffer.ResetWrittenCount();
            if (remaining.Length > 0)
            {
                _buffer.Write(remaining);
            }
        }

        return (IReadOnlyList<byte[]>?)frames ?? Array.Empty<byte[]>();
    }
}
