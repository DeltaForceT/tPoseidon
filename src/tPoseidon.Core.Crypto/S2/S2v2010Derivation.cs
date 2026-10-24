using System.Buffers.Binary;
using tPoseidon.Core.Common;

namespace tPoseidon.Core.Crypto.S2;

public sealed class S2v2010Derivation : IS2CipherDerivation
{
    public byte ValidatePackingFlags(ReadOnlySpan<byte> frame, KeyTable table)
    {

        

        var randKey = frame[S2PacketCipher.OffsetRandKey];
        var packing = frame[S2PacketCipher.OffsetPackingFlags];
        var magic = table.Lookup((randKey << 8) | packing);
        if (magic > 0x0F || (magic & 0x02) == 0)
        {
            throw new InvalidPacketException(
                $"S2 magic check failed: KeyTable[{randKey:X2}{packing:X2}]=0x{magic:X2} " +
                "(must be <= 0x0F and have bit 0x02 set).");
        }
        return (byte)(magic ^ 0x02);
    }

    public ushort RecoverLength(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table)
    {

        

        
        var h = frame;
        var hi = table.Lookup(((h[6] ^ h[7]) << 8) | h[1]);
        var lo = table.Lookup(((h[7] ^ sessionKey) << 8) | h[0]);
        return (ushort)((hi << 8) | lo);
    }

    public ushort RecoverOpcode(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table)
    {

        

        
        var h = frame;
        var hi = table.Lookup(((h[1] ^ sessionKey) << 8) | h[3]);
        var lo = table.Lookup(((h[0] ^ h[6]) << 8) | h[2]);
        return (ushort)((hi << 8) | lo);
    }

    public ushort RecoverSequence(ReadOnlySpan<byte> frame, byte sessionKey, KeyTable table)
    {

        

        
        var h = frame;
        var hi = table.Lookup(((h[3] ^ h[7]) << 8) | h[5]);
        var lo = table.Lookup(((h[2] ^ sessionKey) << 8) | h[4]);
        return (ushort)((hi << 8) | lo);
    }

    public void DecryptPayload(Span<byte> payload, ushort opcode, byte randKey, KeyTable table)
    {

        

        

        

        

        

        

        var derivedKey = (opcode * randKey) & 0xFF;
        for (var i = 0; i < payload.Length; i++)
        {
            var row = ((i & 0xFF) ^ derivedKey) & 0xFF;
            payload[i] = table.Lookup((row << 8) | payload[i]);
        }
    }

    public void EncryptHeader(
        Span<byte> frame,
        ushort length,
        ushort opcode,
        ushort sequence,
        byte randKey,
        PackingFlags flags,
        byte sessionKey,
        KeyTable table)
    {

        

        

        

        

        

        

        

        

        

        

        var lenLo = (byte)(length & 0xFF);
        var lenHi = (byte)((length >> 8) & 0xFF);
        var opLo = (byte)(opcode & 0xFF);
        var opHi = (byte)((opcode >> 8) & 0xFF);
        var seqLo = (byte)(sequence & 0xFF);
        var seqHi = (byte)((sequence >> 8) & 0xFF);

        frame[0] = lenLo;
        frame[1] = lenHi;
        frame[2] = opLo;
        frame[3] = opHi;
        frame[4] = seqLo;
        frame[5] = seqHi;
        frame[S2PacketCipher.OffsetRandKey] = randKey;
        frame[S2PacketCipher.OffsetPackingFlags] = (byte)flags;

        

        

        

        var a = table.Lookup((frame[0] << 8) | frame[2]);
        var bl = table.Lookup((sessionKey << 8) | a);
        var b = table.Lookup((frame[3] << 8) | frame[1]);
        var dl = table.Lookup((frame[6] << 8) | b);
        frame[S2PacketCipher.OffsetHeaderChecksum] = table.Lookup((bl << 8) | dl);

        

        
        var pack = table.InverseLookup(randKey, (byte)((byte)flags | 0x02));
        frame[S2PacketCipher.OffsetPackingFlags] = pack;

        

        frame[1] = table.InverseLookup(randKey ^ pack, lenHi);
        frame[0] = table.InverseLookup(pack ^ sessionKey, lenLo);

        frame[3] = table.InverseLookup(frame[1] ^ sessionKey, opHi);
        frame[2] = table.InverseLookup(frame[0] ^ randKey, opLo);

        frame[5] = table.InverseLookup(frame[3] ^ pack, seqHi);
        frame[4] = table.InverseLookup(frame[2] ^ sessionKey, seqLo);
    }

    public void EncryptPayload(Span<byte> payload, ushort opcode, byte randKey, KeyTable table)
    {

        

        var derivedKey = (opcode * randKey) & 0xFF;
        for (var i = 0; i < payload.Length; i++)
        {
            var row = ((i & 0xFF) ^ derivedKey) & 0xFF;
            payload[i] = table.InverseLookup(row, payload[i]);
        }
    }

    public void DecryptTrailingChecksum(Span<byte> trailer, int payloadLength, ushort opcode, byte randKey, KeyTable table)
    {

        

        
        var derivedKey = (opcode * randKey) & 0xFF;
        for (var j = 0; j < trailer.Length; j++)
        {
            var i = payloadLength + j;
            var row = ((i & 0xFF) ^ derivedKey) & 0xFF;
            trailer[j] = table.Lookup((row << 8) | trailer[j]);
        }
    }

    public void EncryptTrailingChecksum(Span<byte> trailer, int payloadLength, ushort opcode, byte randKey, KeyTable table)
    {
        var derivedKey = (opcode * randKey) & 0xFF;
        for (var j = 0; j < trailer.Length; j++)
        {
            var i = payloadLength + j;
            var row = ((i & 0xFF) ^ derivedKey) & 0xFF;
            trailer[j] = table.InverseLookup(row, trailer[j]);
        }
    }

    public byte UpdateSessionKey(byte oldSessionKey, ushort plaintextChecksum, KeyTable table)
    {

        
        var checksumLow = (byte)(plaintextChecksum & 0xFF);
        var lookup = table.Lookup((checksumLow << 8) | oldSessionKey);
        return (byte)(lookup + oldSessionKey);
    }
}
