using tPoseidon.Core.Game.MyCamp;

namespace tPoseidon.Core.Game.Tests.MyCamp;

public sealed class MyCampShellTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static MyCampShell Shell() => new(1, 7, "owner", 1000000001, "mycamp 1", MyCampRules.ShellZoneBase, 0x4000, 2, 600, 700, new DateTime(2026, 10, 19, 12, 0, 0));

    [Fact]
    public void OpenRefusalsFollowTheServerOrder()
    {
        Assert.Equal(MyCampRules.OpenBusy, MyCampRules.OpenRefusal(true, true, 3, true, true, 1));
        Assert.Equal(MyCampRules.OpenHasCamp, MyCampRules.OpenRefusal(false, true, 3, false, false, 0));
        Assert.Equal(MyCampRules.OpenZoneType, MyCampRules.OpenRefusal(false, false, 3, false, false, 0));
        Assert.Equal(MyCampRules.OpenBusy, MyCampRules.OpenRefusal(false, false, 2, true, false, 0));
        Assert.Equal(MyCampRules.OpenInRegion, MyCampRules.OpenRefusal(false, false, 10, false, true, 1));
        Assert.Equal(MyCampRules.OpenBlockedTile, MyCampRules.OpenRefusal(false, false, 1, false, false, 3));
        Assert.Null(MyCampRules.OpenRefusal(false, false, 2, false, false, 0));
    }

    [Fact]
    public void GuestEntryCodes()
    {
        var shell = Shell();
        Assert.Equal(MyCampRules.EnterNotReady, shell.EnterRefusal(2, "", false, Now));
        shell.Ready = true;
        Assert.Equal(MyCampRules.EnterNotOpen, shell.EnterRefusal(2, "", false, Now));
        shell.Mode = MyCampRules.Chat;
        shell.SetChat("tea", 1, true, "pw");
        Assert.Equal(MyCampRules.EnterWrongPassword, shell.EnterRefusal(2, "no", false, Now));
        Assert.Null(shell.EnterRefusal(2, "no", true, Now));
        Assert.Null(shell.EnterRefusal(2, "pw", false, Now));
        Assert.True(shell.AddGuest(2));
        Assert.Equal(MyCampRules.EnterFull, shell.EnterRefusal(3, "pw", false, Now));
        shell.RemoveGuest(2);
        shell.Ban(2, Now);
        Assert.Equal(MyCampRules.EnterBanned, shell.EnterRefusal(2, "pw", false, Now.AddMinutes(9)));
        Assert.Null(shell.EnterRefusal(2, "pw", false, Now.AddMinutes(10)));
        Assert.False(shell.AddGuest(1));
    }

    [Fact]
    public void ChatInfoIsClampedToSixteen()
    {
        var shell = Shell();
        shell.SetChat(new string('a', 40), 30, false, "ignored");
        Assert.Equal(MyCampRules.MaxGuests, shell.MaxGuests);
        Assert.Equal(MyCampRules.TitleLength, shell.Title.Length);
        Assert.Equal(string.Empty, shell.Password);
    }

    [Fact]
    public void TransferSplitsByBufferSize()
    {
        var records = Enumerable.Range(1, 100).Select(uid => new MyCampItemRecord((ulong)uid, 0, 18001, 0, MyCampRules.StatePlaced)).ToArray();
        var transfer = new MyCampTransfer(records, forGuest: false);
        Assert.Equal(100, transfer.Count);
        var first = transfer.NextChunk(MyCampRules.TransferChunkBytes);
        Assert.Equal(MyCampRules.TransferChunkBytes / 33, first.Count);
        while (!transfer.Done) transfer.NextChunk(MyCampRules.TransferChunkBytes);
        Assert.Empty(transfer.NextChunk(MyCampRules.TransferChunkBytes));
    }

    [Fact]
    public void LayoutKeepsPlacedItemsForTheSave()
    {
        var shell = Shell();
        shell.LoadLayout([new MyCampItemRecord(1, 1, 18001, 0, MyCampRules.StatePlaced, 2400, 2400), new MyCampItemRecord(2, 0, 30000, 0, MyCampRules.StateInventory)]);
        shell.Apply([new MyCampItemRecord(2, 1, 30000, 0, MyCampRules.StatePlaced, 10, 20, 0, 3), new MyCampItemRecord(1, 0, 18001, 0, MyCampRules.StateInventory)]);
        var placed = Assert.Single(shell.Placed());
        Assert.Equal(2UL, placed.ItemUid);
        Assert.Equal(1UL, placed.ParentsUid);
    }
}
