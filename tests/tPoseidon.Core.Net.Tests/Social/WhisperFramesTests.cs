using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class WhisperFramesTests
{
    [Fact]
    public void AWhisperByNameCarriesRoomNameAndText()
    {
        var body = new byte[] { 1, 0, 1, (byte)'B', (byte)'e', 0, (byte)'h', (byte)'i', 0 };

        Assert.True(WhisperFrames.TryRead(body, Encoding.Latin1, out var request));

        Assert.Equal(new WhisperRequest(1, true, 0, "Be", "hi"), request);
    }

    [Fact]
    public void AWhisperByIdCarriesTheTargetClient()
    {
        var body = new byte[] { 1, 0, 0, 7, 0, (byte)'h', 0 };

        Assert.True(WhisperFrames.TryRead(body, Encoding.Latin1, out var request));

        Assert.Equal(new WhisperRequest(1, false, 7, string.Empty, "h"), request);
    }

    [Fact]
    public void TheTargetGetsRoomFlagSenderAndText()
    {
        var bytes = WhisperFrames.ToTargetByName(1, "Ta", "hi", Encoding.Latin1);

        Assert.Equal(new byte[] { 1, 0, 1, (byte)'T', (byte)'a', 0, (byte)'h', (byte)'i', 0 }, bytes);
    }

    [Fact]
    public void TheLobbyReplyIsClientIdThenRoomId()
    {
        Assert.Equal(new byte[] { 5, 0, 1, 0 }, WhisperFrames.Entered(5, 1));
    }
}
