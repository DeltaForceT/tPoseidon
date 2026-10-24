using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class ChatFramesTests
{
    [Fact]
    public void TheClientsChatTextIsTheStringBeforeItsNul()
    {
        Assert.Equal("test", ChatFrames.ReadText(new byte[] { 0x74, 0x65, 0x73, 0x74, 0x00 }, Encoding.Latin1));
    }

    [Fact]
    public void AChatLineIsEntityNameAndText()
    {
        var bytes = ChatFrames.Line(0x2E7E, "Tata", "hi", Encoding.Latin1);

        Assert.Equal(new byte[] { 0x7E, 0x2E, (byte)'T', (byte)'a', (byte)'t', (byte)'a', 0, (byte)'h', (byte)'i', 0 }, bytes);
    }

    [Fact]
    public void TheClientsTextIsTheStringAndItsNul()
    {
        Assert.Equal(new byte[] { 0x74, 0x65, 0x73, 0x74, 0x00 }, ChatFrames.Text("test", Encoding.Latin1));
    }

    [Fact]
    public void AChatLineReadsBackItsEntityNameAndText()
    {
        Assert.Equal(new SpokenLine(0x2E7E, "Tata", "hi"), ChatFrames.ReadLine(ChatFrames.Line(0x2E7E, "Tata", "hi", Encoding.Latin1), Encoding.Latin1));
    }

    [Fact]
    public void AHeadMessageReadsBackItsEntityAndText()
    {
        Assert.Equal(new SpokenHead(0x0102, "hello"), ChatFrames.ReadHead(ChatFrames.Head(0x0102, "hello", Encoding.Latin1), Encoding.Latin1));
    }

    [Fact]
    public void AChatLineWithoutItsTextIsRefused()
    {
        Assert.Throws<tPoseidon.Core.Common.InvalidPacketException>(() => ChatFrames.ReadLine(new byte[] { 0x01, 0x00, (byte)'A', 0x00 }, Encoding.Latin1));
    }

    [Fact]
    public void AHeadMessageKeepsFiftyBytes()
    {
        var bytes = ChatFrames.Head(1, new string('x', 80), Encoding.Latin1);

        Assert.Equal(2 + ChatFrames.HeadMessageLength + 1, bytes.Length);
        Assert.Equal(0, bytes[^1]);
    }
}
