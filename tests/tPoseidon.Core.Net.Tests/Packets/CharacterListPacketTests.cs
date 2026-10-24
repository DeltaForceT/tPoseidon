using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Net.Packets;

namespace tPoseidon.Core.Net.Tests.Packets;

public class CharacterListPacketTests
{
    private static byte[] MakeSyntheticEntry(ulong charId, byte slot, string name)
    {
        var entry = new byte[CharacterListPacket.EntryWidth];
        for (var i = 0; i < 8; i++)
        {
            entry[i] = (byte)((charId >> (i * 8)) & 0xFF);
        }
        entry[8] = slot;
        var nameBytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, entry, 0x09, nameBytes.Length);
        return entry;
    }

    [Fact]
    public void EncodeDecode_RoundTrip_SingleEntry()
    {
        var entry = MakeSyntheticEntry(0xDEADBEEFul, 0, "TestChar1");
        var pkt = new CharacterListPacket
        {
            Entries = new[]
            {
                new CharacterListPacket.Entry { Raw = entry, Name = "TestChar1" },
            },
        };

        var wire = pkt.EncodePayload();
        Assert.Equal(1 + CharacterListPacket.EntryWidth + CharacterListPacket.TrailerLength, wire.Length);
        Assert.Equal(1, wire[0]);

        var dec = CharacterListPacket.DecodePayload(wire);
        Assert.Single(dec.Entries);
        Assert.Equal("TestChar1", dec.Entries[0].Name);
        Assert.Equal(entry, dec.Entries[0].Raw.ToArray());
    }

    [Fact]
    public void EncodeDecode_RoundTrip_MultipleEntries()
    {
        var entries = new[]
        {
            new CharacterListPacket.Entry
            {
                Raw = MakeSyntheticEntry(0x10000001ul, 0, "TestCharA"),
                Name = "TestCharA",
            },
            new CharacterListPacket.Entry
            {
                Raw = MakeSyntheticEntry(0x10000002ul, 1, "TestCharB"),
                Name = "TestCharB",
            },
            new CharacterListPacket.Entry
            {
                Raw = MakeSyntheticEntry(0x10000003ul, 2, "AnotherChar"),
                Name = "AnotherChar",
            },
        };
        var pkt = new CharacterListPacket { Entries = entries };

        var wire = pkt.EncodePayload();
        Assert.Equal(1 + 3 * CharacterListPacket.EntryWidth + CharacterListPacket.TrailerLength, wire.Length);
        Assert.Equal(3, wire[0]);

        var dec = CharacterListPacket.DecodePayload(wire);
        Assert.Equal(3, dec.Entries.Count);
        var expectedNames = new[] { "TestCharA", "TestCharB", "AnotherChar" };
        Assert.Equal(expectedNames, dec.Entries.Select(e => e.Name).ToArray());
    }

    [Fact]
    public void Decode_EmptyList_ReturnsZeroEntries()
    {
        var empty = new byte[1 + CharacterListPacket.TrailerLength];
        var dec = CharacterListPacket.DecodePayload(empty);
        Assert.Empty(dec.Entries);
    }

    [Fact]
    public void EncodeAfterDecode_IsByteIdentical()
    {
        var src = new CharacterListPacket
        {
            Entries = new[]
            {
                new CharacterListPacket.Entry
                {
                    Raw = MakeSyntheticEntry(0xCAFEBABEul, 0, "A"),
                    Name = "A",
                },
                new CharacterListPacket.Entry
                {
                    Raw = MakeSyntheticEntry(0xFEEDFACEul, 1, "LongerNameForTest"),
                    Name = "LongerNameForTest",
                },
            },
        };

        var wire = src.EncodePayload();
        var dec = CharacterListPacket.DecodePayload(wire);
        var reencoded = dec.EncodePayload();

        Assert.Equal(wire, reencoded);
    }

    [Fact]
    public void Decode_LengthMismatch_Throws()
    {
        var bad = new byte[50];
        bad[0] = 2;
        Assert.Throws<InvalidPacketException>(() =>
            CharacterListPacket.DecodePayload(bad));
    }

    [Fact]
    public void Encode_EntryWidthMismatch_Throws()
    {
        var pkt = new CharacterListPacket
        {
            Entries = new[]
            {
                new CharacterListPacket.Entry { Raw = new byte[10], Name = "X" },
            },
        };
        Assert.Throws<InvalidPacketException>(() => pkt.EncodePayload());
    }

    [Fact]
    public void Decode_NameAtFixedOffset0x09()
    {
        var entry = MakeSyntheticEntry(0x123ul, 0, "AtCorrectOffset");
        var wire = new byte[1 + CharacterListPacket.EntryWidth + CharacterListPacket.TrailerLength];
        wire[0] = 1;
        Array.Copy(entry, 0, wire, 1, CharacterListPacket.EntryWidth);

        var dec = CharacterListPacket.DecodePayload(wire);
        Assert.Equal("AtCorrectOffset", dec.Entries[0].Name);
    }

    [Fact]
    public void Decode_TrailerPreservedAcrossRoundTrip()
    {
        var pkt = new CharacterListPacket
        {
            Entries = new[]
            {
                new CharacterListPacket.Entry
                {
                    Raw = MakeSyntheticEntry(0x1ul, 0, "T"),
                    Name = "T",
                },
            },
            Trailer = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF, 0x11, 0x22, 0x33, 0x44, 0x55, 0x08 },
        };
        var wire = pkt.EncodePayload();
        var dec = CharacterListPacket.DecodePayload(wire);
        Assert.Equal(pkt.Trailer, dec.Trailer);
    }
}
