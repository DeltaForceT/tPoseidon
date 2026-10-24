using System.Text;
using tPoseidon.Core.Game.Pets;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class PetSpeechFramesTests
{
    [Fact]
    public void TheSpeechFrameCarriesTheIdTheTerminatedTextAndTheStyle()
    {
        var payload = PetSpeechFrames.Speech(0x0301, new PetUtterance("Hi", 1), Encoding.ASCII);

        Assert.Equal(new byte[] { 0x01, 0x03, 0x48, 0x69, 0x00, 0x01, 0x00, 0x00, 0x00 }, payload);
    }

    [Fact]
    public void TheTextUsesTheGivenEncoding()
    {
        var payload = PetSpeechFrames.Speech(1, new PetUtterance("é", 0), Encoding.Latin1);

        Assert.Equal(new byte[] { 0x01, 0x00, 0xE9, 0x00, 0x00, 0x00, 0x00, 0x00 }, payload);
    }
}
