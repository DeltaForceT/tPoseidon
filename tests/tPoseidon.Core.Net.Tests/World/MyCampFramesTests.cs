using System.Text;
using tPoseidon.Core.Game.MyCamp;
using tPoseidon.Core.Net.World;

namespace tPoseidon.Core.Net.Tests.World;

public sealed class MyCampFramesTests
{
    private static readonly Encoding Text = Encoding.ASCII;

    private static MyCampShell Shell() => new(1, 7, "own", 9, "s", MyCampRules.ShellZoneBase, 0x4001, 2, 600, 700, new DateTime(2026, 10, 19, 12, 30, 15, 250));

    [Fact]
    public void ItemRecordsCarryParentsUidSecond()
    {
        var bytes = MyCampFrames.Items([new MyCampItemRecord(0x11, 0x22, 18002, 0, MyCampRules.StatePlaced, -120, 0, 0, 1), new MyCampItemRecord(0x33, 0, 5, 0, MyCampRules.StateNew)]);
        Assert.Equal(33 + 27, bytes.Length);
        Assert.Equal(0x22UL, BitConverter.ToUInt64(bytes, 8));
        Assert.Equal((ushort)18002, BitConverter.ToUInt16(bytes, 16));
        Assert.Equal(MyCampRules.StatePlaced, bytes[26]);
        Assert.Equal((short)-120, BitConverter.ToInt16(bytes, 27));
        Assert.Equal(1, bytes[32]);
        Assert.True(MyCampFrames.TryReadItems([0x01, 0x00, 0x02, 0x00, .. bytes], out var back));
        Assert.Equal(2, back.Count);
        Assert.Equal((short)-120, back[0].X);
    }

    [Fact]
    public void OwnerEnterHasModeStringsTimeAndNoSittables()
    {
        var shell = Shell();
        shell.Mode = MyCampRules.Chat;
        shell.SetChat("t", 4, false, "");
        var bytes = MyCampFrames.OwnerEnter(shell, Text);
        Assert.Equal(1 + 2 + 2 + 4 + MyCampFrames.SystemTimeSize + 2, bytes.Length);
        Assert.Equal(MyCampRules.Chat, bytes[0]);
        Assert.Equal((ushort)2026, BitConverter.ToUInt16(bytes, 9));
        Assert.Equal((ushort)0, BitConverter.ToUInt16(bytes, bytes.Length - 2));
    }

    [Fact]
    public void OpenAndModeRequestsAreRead()
    {
        Assert.True(MyCampFrames.TryReadOpen([0], Text, out var plain));
        Assert.False(plain.WithChat);
        Assert.True(MyCampFrames.TryReadOpen([1, (byte)'a', 0, 8, 1, (byte)'p', 0], Text, out var chat));
        Assert.Equal(("a", (byte)8, true, "p"), (chat.Chat.Title, chat.Chat.MaxGuests, chat.Chat.HasPassword, chat.Chat.Password));
        Assert.True(MyCampFrames.TryReadMode([1, 0, 1, 0, 0, 0, 0, (byte)'b', 0, 3, 0], Text, out var mode));
        Assert.Equal("b", mode.Chat!.Value.Title);
        Assert.True(MyCampFrames.TryReadMode([1, 0, 0], Text, out var deco));
        Assert.Null(deco.Chat);
    }

    [Fact]
    public void HouseUpdateWritesOnlyFlaggedParts()
    {
        var shell = Shell();
        Assert.Equal(new byte[] { 0x01, 0x40, MyCampRules.HouseFlagModel, 0x9B, 0x75 }, MyCampFrames.HouseUpdate(shell, MyCampRules.HouseFlagModel, Text));
        Assert.Equal(4, MyCampFrames.HouseUpdate(shell, MyCampRules.HouseFlagState, Text).Length);
    }
}
