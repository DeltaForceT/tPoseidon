using tPoseidon.Core.Crypto;
using Xunit;

namespace tPoseidon.Core.Crypto.Tests;

public sealed class SubstitutionTableTests
{
    [Fact]
    public void Identity_ReturnsInputByteForEveryRowAndByte()
    {
        var id = ArraySubstitutionTable.Identity();
        for (var row = 0; row < 16; row++)
        {
            for (var col = 0; col < 256; col++)
            {
                Assert.Equal((byte)col, id.Substitute((byte)row, (byte)col));
            }
        }
    }

    [Fact]
    public void DummyPair_DecryptInvertsEncrypt_ForAllRowsAndBytes()
    {
        var (enc, dec) = ArraySubstitutionTable.GenerateDummyPair(seed: 1234);
        for (var row = 0; row < 16; row++)
        {
            for (var col = 0; col < 256; col++)
            {
                var ciphered = enc.Substitute((byte)row, (byte)col);
                var recovered = dec.Substitute((byte)row, ciphered);
                Assert.Equal((byte)col, recovered);
            }
        }
    }

    [Fact]
    public void DummyPair_EncryptEachRowIsAPermutationOfBytes()
    {
        var (enc, _) = ArraySubstitutionTable.GenerateDummyPair(seed: 42);
        for (var row = 0; row < 16; row++)
        {
            var seen = new HashSet<byte>();
            for (var col = 0; col < 256; col++)
            {
                Assert.True(seen.Add(enc.Substitute((byte)row, (byte)col)),
                    $"Row {row} column {col} is not a permutation.");
            }
            Assert.Equal(256, seen.Count);
        }
    }

    [Fact]
    public void WrongSize_ThrowsOnConstruction()
    {
        Assert.Throws<ArgumentException>(() => new ArraySubstitutionTable(new byte[100]));
        Assert.Throws<ArgumentException>(() => new ArraySubstitutionTable(new byte[4097]));
    }

    [Fact]
    public void KeyNibble_AboveRange_Throws()
    {
        var id = ArraySubstitutionTable.Identity();
        Assert.Throws<ArgumentOutOfRangeException>(() => id.Substitute(0x10, 0x00));
    }

    [Fact]
    public void SourceBytesAreCloned_SoExternalMutationDoesNotLeakIn()
    {
        var buf = new byte[4096];
        var table = new ArraySubstitutionTable(buf);
        buf[0] = 0xFF;
        Assert.Equal(0x00, table.Substitute(0x00, 0x00));
    }
}
