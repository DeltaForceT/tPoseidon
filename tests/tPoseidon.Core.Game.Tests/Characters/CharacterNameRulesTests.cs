using System.Text;
using tPoseidon.Core.Game.Characters;

namespace tPoseidon.Core.Game.Tests.Characters;

public sealed class CharacterNameRulesTests
{
    private static bool Accepts(string name) => CharacterNameRules.Accepts(Encoding.ASCII.GetBytes(name));

    [Theory]
    [InlineData("abcd")]
    [InlineData("Newname1")]
    [InlineData("1234567890123456")]
    public void AsciiNamesOfFourToSixteenBytesAreAccepted(string name) => Assert.True(Accepts(name));

    [Theory]
    [InlineData("abc")]
    [InlineData("12345678901234567")]
    [InlineData("")]
    public void TheLengthStaysBetweenFourAndSixteenBytes(string name) => Assert.False(Accepts(name));

    [Theory]
    [InlineData(".name")]
    [InlineData("'name")]
    [InlineData("/name")]
    public void ALeadingDotQuoteOrSlashIsRefused(string name) => Assert.False(Accepts(name));

    [Theory]
    [InlineData("na me")]
    [InlineData("na`me")]
    [InlineData("na,me")]
    [InlineData("na'me")]
    [InlineData("name.")]
    public void SpacesBackticksCommasQuotesAndATrailingDotAreRefused(string name) => Assert.False(Accepts(name));

    [Fact]
    public void AControlByteIsRefused() => Assert.False(CharacterNameRules.Accepts(new byte[] { 0x61, 0x62, 0x01, 0x63, 0x64 }));

    [Fact]
    public void AValidHangulPairCountsAsTwoBytes()
    {
        Assert.True(CharacterNameRules.Accepts(new byte[] { 0xB0, 0xA1, 0xB0, 0xA2 }));
        Assert.False(CharacterNameRules.Accepts(new byte[] { 0xB0, 0xA1, 0x61 }));
        Assert.False(CharacterNameRules.Accepts(new byte[] { 0xB0, 0xA1, 0xB0, 0xA2, 0xB0, 0xA3, 0xB0, 0xA4, 0xB0, 0xA5, 0xB0, 0xA6, 0xB0, 0xA7, 0xB0, 0xA8, 0xB0, 0xA9 }));
    }

    [Theory]
    [InlineData(0xB0, 0x5B)]
    [InlineData(0xB0, 0x7B)]
    [InlineData(0x81, 0x41)]
    public void ThePairsTheOriginalRangeTestsNeverCatchAreAccepted(byte lead, byte trail) =>
        Assert.True(CharacterNameRules.Accepts(new byte[] { 0x61, 0x62, lead, trail }));

    [Theory]
    [InlineData(0xFF, 0xA1)]
    [InlineData(0xB0, 0xFF)]
    [InlineData(0xC9, 0xA1)]
    [InlineData(0xFE, 0xA1)]
    [InlineData(0xA4, 0xD4)]
    [InlineData(0xC8, 0x50)]
    [InlineData(0xC6, 0x60)]
    public void TheRejectedDoubleBytePairsAreRefused(byte lead, byte trail) =>
        Assert.False(CharacterNameRules.Accepts(new byte[] { 0x61, 0x62, lead, trail }));
}
