using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Packets;
using tPoseidon.Core.Net.S2;
using Xunit;

namespace tPoseidon.Core.Net.Tests.S2;

public sealed class CharacterListBuilderTests
{
    private static Character MakeChar(
        ulong id, byte slot, string name,
        Species species = Species.Buffalo,
        FType ftype = FType.Power,
        ushort level = 1,
        byte jobTier = 0x01) => new()
    {
        CharacterId = id,
        OwnerAccountId = 1,
        Slot = slot,
        Name = name,
        Species = species,
        FType = ftype,
        Level = level,
        JobTier = jobTier,
    };

    [Fact]
    public void Build_EmptyList_ReturnsZeroEntries()
    {
        var pkt = CharacterListBuilder.Build(Array.Empty<Character>());
        Assert.Empty(pkt.Entries);
    }

    [Fact]
    public void Build_PlacesCharIdAtOffsetZero_AsLittleEndianU64()
    {
        var pkt = CharacterListBuilder.Build(new[] { MakeChar(0x12345678ul, 0, "Reference") });
        var entry = pkt.Entries[0].Raw.ToArray();
        Assert.Equal(0x78, entry[0]);
        Assert.Equal(0x56, entry[1]);
        Assert.Equal(0x34, entry[2]);
        Assert.Equal(0x12, entry[3]);
        Assert.Equal(0x00, entry[4]);
        Assert.Equal(0x00, entry[5]);
        Assert.Equal(0x00, entry[6]);
        Assert.Equal(0x00, entry[7]);
    }

    [Fact]
    public void Build_PlacesSlotAtOffsetEight()
    {
        var pkt = CharacterListBuilder.Build(new[] { MakeChar(0x100, 7, "Test") });
        Assert.Equal(7, pkt.Entries[0].Raw.Span[8]);
    }

    [Fact]
    public void Build_PlacesNameAtOffset0x09_NulTerminated()
    {
        var pkt = CharacterListBuilder.Build(new[] { MakeChar(0x100, 0, "Hello") });
        var entry = pkt.Entries[0].Raw.Span;
        const int nameOffset = 0x09;
        Assert.Equal((byte)'H', entry[nameOffset + 0]);
        Assert.Equal((byte)'e', entry[nameOffset + 1]);
        Assert.Equal((byte)'l', entry[nameOffset + 2]);
        Assert.Equal((byte)'l', entry[nameOffset + 3]);
        Assert.Equal((byte)'o', entry[nameOffset + 4]);
        Assert.Equal((byte)0,   entry[nameOffset + 5]);
    }

    [Fact]
    public void Build_AllEntriesHaveExactly154Bytes()
    {
        var pkt = CharacterListBuilder.Build(new[]
        {
            MakeChar(0x100, 0, "A"),
            MakeChar(0x101, 1, "B"),
            MakeChar(0x102, 2, "C"),
        });
        Assert.Equal(CharacterListPacket.EntryWidth, CharacterListBuilder.EntryWidth);
        foreach (var e in pkt.Entries)
        {
            Assert.Equal(CharacterListPacket.EntryWidth, e.Raw.Length);
        }
    }

    [Fact]
    public void Build_PlacesSpeciesAndFType_FreshChar_PerSection22()
    {
        var pkt = CharacterListBuilder.Build(new[] {
            MakeChar(0x100, 0, "Reference", Species.Dragon, FType.Magic) });
        var entry = pkt.Entries[0].Raw.Span;

        Assert.Equal((byte)Species.Dragon, entry[0x1D]);
        Assert.Equal(0, entry[0x1E]);

        Assert.Equal(0, entry[0x1F]);
        Assert.Equal(0, entry[0x20]);

        Assert.Equal(1, entry[0x21]);

        Assert.Equal((byte)Species.Dragon, entry[0x22]);

        Assert.Equal(0, entry[0x23]);
    }

    [Fact]
    public void Build_PlacesSecondJobClass_OnceJobTierAdvances()
    {
        var pkt = CharacterListBuilder.Build(new[] {
            MakeChar(0x100, 0, "Reference", Species.Dragon, FType.Magic, jobTier: 0x02) });
        var entry = pkt.Entries[0].Raw.Span;

        Assert.Equal((byte)((byte)Species.Dragon + 8), entry[0x1F]);
        Assert.Equal(0, entry[0x20]);
        Assert.Equal(12, entry[0x23]);
        Assert.Equal((byte)0x02, entry[0x24]);
    }

    [Fact]
    public void Build_PlacesLevelAtOffset0x2B_AsU16Le()
    {
        var pkt = CharacterListBuilder.Build(new[] { MakeChar(0x100, 0, "X", level: 0x1234) });
        var entry = pkt.Entries[0].Raw.Span;
        Assert.Equal(0x34, entry[0x2B]);
        Assert.Equal(0x12, entry[0x2C]);
    }

    [Fact]
    public void Build_NameTooLong_Throws()
    {
        var longName = new string('X', 24);
        Assert.Throws<InvalidOperationException>(() =>
            CharacterListBuilder.Build(new[] { MakeChar(0x100, 0, longName) }));
    }

    [Fact]
    public void Build_RoundTripsThroughEncodeDecode()
    {
        var src = CharacterListBuilder.Build(new[]
        {
            MakeChar(0x100, 0, "First", Species.Buffalo, FType.Power),
            MakeChar(0x200, 1, "Second", Species.Lion, FType.Power),
        });
        var wire = src.EncodePayload();
        var dec = CharacterListPacket.DecodePayload(wire);

        Assert.Equal(2, dec.Entries.Count);
        Assert.Equal("First", dec.Entries[0].Name);
        Assert.Equal("Second", dec.Entries[1].Name);
    }

    [Fact]
    public void Build_TotalPayloadIs1Plus156xN_Plus12Trailer()
    {
        var pkt = CharacterListBuilder.Build(new[]
        {
            MakeChar(0x100, 0, "A"),
            MakeChar(0x101, 1, "B"),
        });
        var wire = pkt.EncodePayload();
        Assert.Equal(1 + (156 * 2) + 12, wire.Length);
        Assert.Equal(2, wire[0]);
        var trailerStart = wire.Length - 12;
        for (var i = trailerStart; i < wire.Length - 1; i++)
        {
            Assert.Equal(0, wire[i]);
        }
        Assert.Equal((byte)8, wire[wire.Length - 1]);
    }
}
