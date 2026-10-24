using tPoseidon.Core.Game.Characters;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class LookChangeFramesTests
{
    private static readonly ushort[] Looks = [2, 2, 10, 10, 20, 20];
    private static readonly byte[] LooksBytes = [2, 0, 2, 0, 10, 0, 10, 0, 20, 0, 20, 0];
    private static readonly byte[] ChangedBytes = [0x34, 0x12, 20, 0, 20, 5, 3, 1];
    private static readonly byte[] Request = [2, 0];

    [Fact]
    public void LooksAreSixWords()
    {
        Assert.Equal(LooksBytes, LookChangeFrames.Looks(Looks));
    }

    [Fact]
    public void ChangedEndsWithTheAppearanceFlag()
    {
        Assert.Equal(ChangedBytes, LookChangeFrames.Changed(0x1234, new MakeUp(20, 20, 5), 3));
    }

    [Fact]
    public void RequestIsOneWord()
    {
        Assert.True(LookChangeFrames.TryReadRequest(Request, out var state));
        Assert.Equal(2, state);
        Assert.False(LookChangeFrames.TryReadRequest(Request.AsSpan(0, 1), out _));
    }
}
