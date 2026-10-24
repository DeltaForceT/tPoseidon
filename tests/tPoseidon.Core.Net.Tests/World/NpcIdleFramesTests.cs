using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.LibConfig;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class NpcIdleFramesTests
{
    private const int KoreanCodePage = 949;

    static NpcIdleFramesTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void Facing_IsOneNpcStateFrame()
    {
        var frames = NpcIdleFrames.Encode(
            new NpcFacingEvent(0x0201, NpcIdleCycle.DirectionAt225Degrees), Encoding.ASCII);

        var frame = Assert.Single(frames);
        Assert.Equal(Opcode.S2C_NPC_STATE, frame.Opcode);
        Assert.Equal(new byte[] { 0x01, 0x02, 0xA0 }, frame.Payload);
    }

    [Fact]
    public void MotionWithoutSpeech_IsOneMotionFrame()
    {
        var step = new NpcActionScriptStep(Action: 3, Loop: 0, AniTime: 3, Speech: string.Empty);

        var frames = NpcIdleFrames.Encode(new NpcMotionEvent(0x0201, step), Encoding.ASCII);

        var frame = Assert.Single(frames);
        Assert.Equal(Opcode.S2C_OP_01CF_TBD, frame.Opcode);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03, 0x03, 0x00, 0x00 }, frame.Payload);
    }

    [Fact]
    public void MotionWithSpeech_IsTheMotionFrameThenTheSpeechFrame()
    {
        var step = new NpcActionScriptStep(Action: 1, Loop: 1, AniTime: 5, Speech: "Hi!");

        var frames = NpcIdleFrames.Encode(new NpcMotionEvent(0x0201, step), Encoding.ASCII);

        Assert.Equal(2, frames.Count);
        Assert.Equal(Opcode.S2C_OP_01CF_TBD, frames[0].Opcode);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x01, 0x05, 0x00, 0x01 }, frames[0].Payload);
        Assert.Equal(Opcode.S2C_OP_01D0_TBD, frames[1].Opcode);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x48, 0x69, 0x21, 0x00 }, frames[1].Payload);
    }

    [Fact]
    public void Speech_IsWrittenInTheGivenTextEncoding()
    {
        var korean = Encoding.GetEncoding(KoreanCodePage);
        var step = new NpcActionScriptStep(Action: 1, Loop: 1, AniTime: 5, Speech: "가");

        var frames = NpcIdleFrames.Encode(new NpcMotionEvent(0x0201, step), korean);

        Assert.Equal(new byte[] { 0x01, 0x02, 0xB0, 0xA1, 0x00 }, frames[1].Payload);
    }
}
