using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class CharacterPetReinforceFramesTests
{
    [Fact]
    public void RequestIsReinforceItemThenPet()
    {
        var body = new byte[16];
        body[0] = 1;
        body[8] = 2;
        Assert.True(CharacterPetReinforceFrames.TryReadRequest(body, out var request));
        Assert.Equal(new CharacterPetReinforceRequest(1, 2), request);
        Assert.False(CharacterPetReinforceFrames.TryReadRequest(body.AsSpan(0, 15), out _));
    }

    [Fact]
    public void ResultCountIsTwoBytes()
    {
        var take = new BagOperation(QuestResultFrames.Removed, 1, 19832, 0x10, 0);
        var frame = CharacterPetReinforceFrames.Result(new[] { take });
        Assert.Equal(new byte[] { 1, 0 }, frame[..2]);
        Assert.Equal(QuestResultFrames.ItemOperation(take), frame[2..]);
        Assert.Equal(new byte[] { 7, 0 }, CharacterPetReinforceFrames.Refused(7));
    }
}
