using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Tools.PacketSniffer;

internal sealed class S2StreamFramer
{
    private readonly KeyTable _table;
    private readonly IS2CipherDerivation _derivation;
    private byte[] _buffer = new byte[8192];
    private int _start;
    private int _length;

    public S2StreamFramer(KeyTable table, IS2CipherDerivation derivation, byte initialSessionKey)
    {
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

        public List<byte[]> DrainFrames()
    {
        var frames = new List<byte[]>();
        while (TryDrainOne(out var frame))
        {
            frames.Add(frame);
        }
        Compact();
        return frames;
    }

        public bool TryDrainOne(out byte[] frame)
    {
        frame = Array.Empty<byte>();
        if (_length < S2PacketCipher.MinimumFrameSize) return false;

        ReadOnlySpan<byte> head = _buffer.AsSpan(_start, _length);
        try
        {
            _derivation.ValidatePackingFlags(head, _table);
        }
        catch (tPoseidon.Core.Common.InvalidPacketException)
        {
            return false;
        }

        var len = _derivation.RecoverLength(head, SessionKey, _table);
        if (len < S2PacketCipher.MinimumFrameSize) return false;
        if (len > 65535) return false;
        if (_length < len) return false;

        frame = new byte[len];
        head[..len].CopyTo(frame);
        _start += len;
        _length -= len;
        return true;
    }

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
