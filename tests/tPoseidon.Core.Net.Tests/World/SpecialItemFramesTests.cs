using System.Text;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class SpecialItemFramesTests
{
    [Fact]
    public void TheFormRefusalIsKindEightWithTwoEmptyWords() =>
        Assert.Equal(new byte[] { 8, 0, 0, 0, 0 }, SpecialItemFrames.FormRefused());

    [Fact]
    public void TheHairAnswersCarryTheColour()
    {
        Assert.Equal(new byte[] { 0x07 }, SpecialItemFrames.Hair(7));
        Assert.Equal(new byte[] { 0x23, 0x01, 0x07 }, SpecialItemFrames.HairSeenBy(0x0123, 7));
    }

    [Fact]
    public void TheGenderAnswerIsEntityTypeJobAndZero() =>
        Assert.Equal(new byte[] { 0x23, 0x01, 0x05, 0x00, 0x05, 0x00 }, SpecialItemFrames.GenderChangedFor(0x0123, 5, 5));

    [Fact]
    public void TheSkillResetAnswerIsSkillAndRefund() =>
        Assert.Equal(new byte[] { 0x50, 0x04, 0x07, 0x00 }, SpecialItemFrames.SkillResetFor(1104, 7));

    [Fact]
    public void TheStatResetAnswersCarryTheThirtyThreeByteBlock()
    {
        var block = new CharacterStatBlock(Enumerable.Range(1, 12).Select(point => (short)point).ToArray(), 9, 10, 5, 50, 1);

        var single = SpecialItemFrames.StatBlock(block);
        var all = SpecialItemFrames.StatResetAllFor(block, 0x01020304);

        Assert.Equal(CharacterStatBlock.Size, single.Length);
        Assert.Equal(CharacterStatBlock.Size + 4, all.Length);
        Assert.Equal(single, all[..CharacterStatBlock.Size]);
        Assert.Equal(new byte[] { 4, 3, 2, 1 }, all[CharacterStatBlock.Size..]);
        Assert.Equal(1, single[0]);
        Assert.Equal(9, single[24]);
    }

    [Fact]
    public void TheGrowthResetAnswerIsTheCharacterInfoThenTheStatBlock()
    {
        var info = Enumerable.Range(0, 69).Select(value => (byte)value).ToArray();
        var block = new CharacterStatBlock(new short[12], 40, 10, 5, 11, 1);

        var payload = SpecialItemFrames.GrowthResetFor(info, block);

        Assert.Equal(69 + CharacterStatBlock.Size, payload.Length);
        Assert.Equal(info, payload[..69]);
        Assert.Equal(SpecialItemFrames.StatBlock(block), payload[69..]);
    }

    [Fact]
    public void TheStorageAnswerIsTheSlotCount() =>
        Assert.Equal(new byte[] { 8 }, SpecialItemFrames.StorageExpandedBy(8));

    [Fact]
    public void ACodeIsAFourByteWord() =>
        Assert.Equal(new byte[] { 0xFB, 0x03, 0x00, 0x00 }, SpecialItemFrames.Code(1019));

    [Fact]
    public void TheRenameAnswerIsTheItemOperationRecord()
    {
        var removed = new BagOperation(QuestResultFrames.Removed, 1, 19711, 0x1_0000_0010, 0);

        Assert.Equal(QuestResultFrames.ItemOperation(removed), SpecialItemFrames.RenamedWith(removed));
    }

    [Fact]
    public void TheMegaphoneAnswerIsTheItemOperationThenTheQueueLength()
    {
        var changed = new BagOperation(QuestResultFrames.StackChanged, 1, 19656, 0x1_0000_0010, 2);

        var payload = SpecialItemFrames.MegaphoneSentWith(changed, 3);
        var record = QuestResultFrames.ItemOperation(changed);

        Assert.Equal(record, payload[..record.Length]);
        Assert.Equal(new byte[] { 3, 0, 0, 0 }, payload[record.Length..]);
    }

    [Fact]
    public void TheShoutIsStyleThenNulTerminatedText() =>
        Assert.Equal(new byte[] { 0x02, 0x00, (byte)'h', (byte)'i', 0x00 }, SpecialItemFrames.MegaphoneShouted(2, "hi", Encoding.ASCII));

    [Fact]
    public void ANameFieldHoldsOneToSeventeenBytes()
    {
        Assert.True(SpecialItemFrames.TryReadName(Encoding.ASCII.GetBytes("Name\0"), out var name));
        Assert.Equal("Name", Encoding.ASCII.GetString(name));
        Assert.True(SpecialItemFrames.TryReadName(new byte[17], out _));
        Assert.False(SpecialItemFrames.TryReadName([], out _));
        Assert.False(SpecialItemFrames.TryReadName(new byte[18], out _));
    }

    [Fact]
    public void AMegaphoneRequestIsMicrophoneUidNameAndText()
    {
        var data = new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 }
            .Concat(Encoding.ASCII.GetBytes("Tester\0Hello all\0")).ToArray();

        Assert.True(SpecialItemFrames.TryReadMegaphone(data, out var request));

        Assert.Equal(0x0000_0001_0000_0001UL, request.WireMicrophoneUid);
        Assert.Equal("Tester", Encoding.ASCII.GetString(request.Name));
        Assert.Equal("Hello all", Encoding.ASCII.GetString(request.Text));
    }

    [Theory]
    [InlineData(new byte[] { 1, 0, 0, 0, 1, 0, 0 })]
    [InlineData(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 65, 0 })]
    [InlineData(new byte[] { 1, 0, 0, 0, 1, 0, 0, 0, 65, 0, 66 })]
    public void ATruncatedMegaphoneRequestIsRefused(byte[] data) =>
        Assert.False(SpecialItemFrames.TryReadMegaphone(data, out _));

    [Fact]
    public void AFriendFinderTargetIsSignedXSignedYAndMap()
    {
        Assert.True(SpecialItemFrames.TryReadFriendFinder(new byte[] { 0xFE, 0xFF, 0x0A, 0x00, 0x94, 0x00 }, out var target));

        Assert.Equal(new FriendFinderTarget(-2, 10, 148), target);
        Assert.False(SpecialItemFrames.TryReadFriendFinder(new byte[5], out _));
    }

    [Fact]
    public void TheShortRequestsAreRefused()
    {
        Assert.True(SpecialItemFrames.TryReadWord(new byte[] { 0x34, 0x12 }, out var word));
        Assert.Equal(0x1234, word);
        Assert.False(SpecialItemFrames.TryReadWord(new byte[1], out _));
        Assert.True(SpecialItemFrames.TryReadAdvancedZoneIndex(new byte[] { 3, 0, 0, 0 }, out var index));
        Assert.Equal(3u, index);
        Assert.False(SpecialItemFrames.TryReadAdvancedZoneIndex(new byte[3], out _));
    }
}
