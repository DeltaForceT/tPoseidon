using tPoseidon.Core.Common;
using tPoseidon.Core.Crypto.Trident;

namespace tPoseidon.Core.Net.Trident;

public sealed class TridentStreamFramer
{
    private readonly KeyTable _table;
    private readonly ITridentCipherDerivation _derivation;
    private byte[] _buffer = new byte[8192];
    private int _start;
    private int _length;

    public TridentStreamFramer(KeyTable table, ITridentCipherDerivation derivation, byte initialSessionKey)
    {
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(derivation);
        _table = table;
        _derivation = derivation;
        SessionKey = initialSessionKey;
    }

    public byte SessionKey { get; private set; }

    public int Buffered => _length;

        public void OnSessionKeyAdvanced(byte newKey) => SessionKey = newKey;

        public void Feed(ReadOnlySpan<byte> incoming)
    {
        if (incoming.IsEmpty) return;
        EnsureCapacity(_length + incoming.Length);
        incoming.CopyTo(_buffer.AsSpan(_start + _length));
        _length += incoming.Length;
    }

        public bool TryDrainOne(out byte[] frame) => TryDrain(out frame) == TridentFrameStatus.Frame;

    public TridentFrameStatus TryDrain(out byte[] frame, int maxFrameBytes = ushort.MaxValue)
    {
        frame = Array.Empty<byte>();
        if (_length < TridentPacketCipher.MinimumFrameSize) return TridentFrameStatus.NeedMore;

        ReadOnlySpan<byte> head = _buffer.AsSpan(_start, _length);
        try
        {
            _derivation.ValidatePackingFlags(head, _table);
        }
        catch (InvalidPacketException)
        {
            return TridentFrameStatus.Malformed;
        }

        var len = _derivation.RecoverLength(head, SessionKey, _table);
        if (len < TridentPacketCipher.MinimumFrameSize) return TridentFrameStatus.Malformed;
        if (len > maxFrameBytes) return TridentFrameStatus.Oversized;
        if (_length < len) return TridentFrameStatus.NeedMore;

        frame = new byte[len];
        head[..len].CopyTo(frame);
        _start += len;
        _length -= len;
        return TridentFrameStatus.Frame;
    }

    public ushort PeekLength() =>
        _length < TridentPacketCipher.MinimumFrameSize
            ? (ushort)0
            : _derivation.RecoverLength(_buffer.AsSpan(_start, _length), SessionKey, _table);

    private void Compact()
    {
        if (_start == 0) return;
        if (_length > 0)
        {
            Array.Copy(_buffer, _start, _buffer, 0, _length);
        }
        _start = 0;
    }

    private void EnsureCapacity(int needed)
    {
        if (_start + needed > _buffer.Length) Compact();
        if (needed <= _buffer.Length) return;
        var grown = Math.Max(needed, _buffer.Length * 2);
        Array.Resize(ref _buffer, grown);
    }
}

public enum TridentFrameStatus
{
    Frame,
    NeedMore,
    Malformed,
    Oversized,
}
