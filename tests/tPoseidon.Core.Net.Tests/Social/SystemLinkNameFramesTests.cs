using System.Text;
using tPoseidon.Core.Net.Social;

namespace tPoseidon.Core.Net.Tests.Social;

public sealed class SystemLinkNameFramesTests
{
    [Fact]
    public void ARenameCarriesTheCharacterAndBothNames()
    {
        var body = SystemLinkFrames.CharacterRenamedFor(0x1122334455667788, "Oldname", "Newname", Encoding.ASCII);

        Assert.Equal(new byte[] { 0x88, 0x77, 0x66, 0x55, 0x44, 0x33, 0x22, 0x11 }, body[..8]);
        Assert.Equal((0x1122334455667788UL, "Oldname", "Newname"), SystemLinkFrames.ReadCharacterRenamed(body, Encoding.ASCII));
    }

    [Fact]
    public void AMegaphoneCarriesTheCharacterTheStyleAndTheMessage()
    {
        var body = SystemLinkFrames.MegaphoneFor(7, 2, "Tester : hello", Encoding.ASCII);

        Assert.Equal((7UL, (ushort)2, "Tester : hello"), SystemLinkFrames.ReadMegaphone(body, Encoding.ASCII));
    }

    [Fact]
    public void ThePartyRenameNoticeIsMemberIdThenName() =>
        Assert.Equal(new byte[] { 0x05, 0x00, (byte)'N', (byte)'e', (byte)'w', 0x00 }, PartyFrames.MemberNameFor(5, "New", Encoding.ASCII));
}
