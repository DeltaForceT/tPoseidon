using System.Text;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class GiftFramesTests
{
    private static readonly Encoding Text = Encoding.Latin1;

    [Fact]
    public void AcceptReadsTheCodeMessageAndSender()
    {
        byte[] payload = [0x2A, 0, 0, 0, (byte)'h', (byte)'i', 0, (byte)'B', (byte)'e', (byte)'a', 0];
        Assert.True(GiftFrames.TryReadAccept(payload, Text, out var request));
        Assert.Equal(new GiftAcceptRequest(42, "hi", "Bea"), request);
        Assert.False(GiftFrames.TryReadAccept(payload.AsSpan(0, 8), Text, out _));
    }

    [Fact]
    public void AcceptResultIsOneWord()
    {
        Assert.Equal(new byte[] { 1, 0, 0, 0 }, GiftFrames.AcceptResult(1));
    }

    [Fact]
    public void PendingListWritesTheOverflowFlagCountAndRows()
    {
        var at = new DateTimeOffset(2025, 3, 26, 9, 14, 0, TimeSpan.Zero);
        var row = new GiftPendingRow(7, 900, "Gus", at, "hi", 3);
        var bytes = GiftFrames.Pending([row], Text);
        var expected = new List<byte>();
        expected.AddRange(BitConverter.GetBytes(0U));
        expected.AddRange(BitConverter.GetBytes(1U));
        expected.AddRange(BitConverter.GetBytes(7U));
        expected.AddRange(BitConverter.GetBytes(900U));
        expected.AddRange(Text.GetBytes("Gus\0" + GiftFrames.Date(at) + "\0hi\0"));
        expected.AddRange(BitConverter.GetBytes(3U));
        expected.AddRange(BitConverter.GetBytes(GiftFrames.ClientRowExtension));
        Assert.Equal(expected.ToArray(), bytes);
    }

    [Fact]
    public void ListsCapAtOneHundredRowsAndRaiseTheFlag()
    {
        var at = DateTimeOffset.UnixEpoch;
        var rows = Enumerable.Range(0, GiftItemRules.ListRowLimit + 1).Select(i => new GiftSentRow((uint)i, at, "R", "", i % 2 == 0, 1)).ToArray();
        var bytes = GiftFrames.Sent(rows, Text);
        Assert.Equal(1U, BitConverter.ToUInt32(bytes, 0));
        Assert.Equal(100U, BitConverter.ToUInt32(bytes, 4));
    }

    [Fact]
    public void DateUsesYearMonthDayHourMinuteSecond()
    {
        Assert.Matches(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$", GiftFrames.Date(DateTimeOffset.UnixEpoch));
        var local = new DateTimeOffset(2014, 1, 28, 12, 34, 29, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2014, 1, 28, 12, 34, 29)));
        Assert.Equal("2014-01-28 12:34:00", GiftFrames.Date(local));
        Assert.Equal("2014-01-28 12:35:00", GiftFrames.Date(local.AddSeconds(1)));
    }
}
