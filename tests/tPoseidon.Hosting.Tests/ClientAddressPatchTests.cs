using System.Text;

namespace tPoseidon.Hosting.Tests;

public sealed class ClientAddressPatchTests
{
    private const int FirstMarker = 0x40;

    private static ClientLobbySlot Korean => ClientAddressPatch.Slots.Single(slot => slot.Service == ClientFolder.KoreanService);

    private static ClientLobbySlot English => ClientAddressPatch.Slots.Single(slot => slot.Service == ClientFolder.EnglishService);

    private static byte[] FakeClient()
    {
        var bytes = new byte[0xC0];
        bytes.AsSpan().Fill(0xCC);
        var offset = FirstMarker;
        foreach (var lobby in ClientAddressPatch.Slots)
        {
            Text(bytes, offset, lobby.PrecedingAddress);
            Text(bytes, offset + ClientAddressPatch.SlotBytes, lobby.BuiltInAddress);
            offset += 2 * ClientAddressPatch.SlotBytes;
        }

        return bytes;
    }

    private static void Text(byte[] bytes, int offset, string text)
    {
        bytes.AsSpan(offset, ClientAddressPatch.SlotBytes).Clear();
        Encoding.ASCII.GetBytes(text).CopyTo(bytes, offset);
    }

    [Fact]
    public void EachLobbySlotFollowsItsTestServerAddress()
    {
        var client = FakeClient();

        var korean = ClientAddressPatch.FindSlot(client, Korean);
        var english = ClientAddressPatch.FindSlot(client, English);

        Assert.Equal(FirstMarker + ClientAddressPatch.SlotBytes, korean);
        Assert.Equal(FirstMarker + 3 * ClientAddressPatch.SlotBytes, english);
        Assert.Equal(Korean.BuiltInAddress, ClientAddressPatch.ReadAddress(client, korean));
        Assert.Equal(English.BuiltInAddress, ClientAddressPatch.ReadAddress(client, english));
    }

    [Fact]
    public void WritingAnAddressClearsTheRestOfTheSlot()
    {
        var client = FakeClient();
        var slot = ClientAddressPatch.FindSlot(client, English);

        ClientAddressPatch.WriteAddress(client, slot, "127.0.0.1");

        Assert.Equal("127.0.0.1", ClientAddressPatch.ReadAddress(client, slot));
        Assert.All(client.AsSpan(slot + 9, ClientAddressPatch.SlotBytes - 9).ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(0xCC, client[slot + ClientAddressPatch.SlotBytes]);
        Assert.Equal(slot, ClientAddressPatch.FindSlot(client, English));
    }

    [Fact]
    public void ALongestAddressFitsAndOtherTextIsRefused()
    {
        var client = FakeClient();
        var slot = ClientAddressPatch.FindSlot(client, Korean);

        ClientAddressPatch.WriteAddress(client, slot, "255.255.255.255");
        Assert.Equal("255.255.255.255", ClientAddressPatch.ReadAddress(client, slot));
        Assert.Throws<ArgumentException>(() => ClientAddressPatch.WriteAddress(client, slot, "play.example.org"));
    }

    [Fact]
    public void AClientWithoutTheMarkersIsNotTouched()
    {
        using var temp = new TempFolder();
        var exe = temp.Combine("Trickster.bin");
        File.WriteAllBytes(exe, new byte[0x80]);

        Assert.Equal(-1, ClientAddressPatch.FindSlot(new byte[0x80], English));
        Assert.Throws<HostingException>(() => ClientAddressPatch.Apply(exe, "127.0.0.1", ClientAddressPatch.ClientLobbyPort));
        Assert.False(File.Exists(exe + ClientAddressPatch.BackupSuffix));
    }

    [Fact]
    public void ApplyPatchesBothSlotsKeepsTheOriginalAndRepatchesInPlace()
    {
        using var temp = new TempFolder();
        var exe = temp.Combine("Trickster.bin");
        File.WriteAllBytes(exe, FakeClient());

        var first = ClientAddressPatch.Apply(exe, "127.0.0.1", ClientAddressPatch.ClientLobbyPort);
        var again = ClientAddressPatch.Apply(exe, "127.0.0.1", ClientAddressPatch.ClientLobbyPort);
        var moved = ClientAddressPatch.Apply(exe, "192.0.2.10", ClientAddressPatch.ClientLobbyPort);

        Assert.True(first.Changed);
        Assert.Equal(exe + ClientAddressPatch.BackupSuffix, first.BackupPath);
        Assert.False(again.Changed);
        Assert.True(moved.Changed);
        Assert.Null(moved.BackupPath);
        var patched = File.ReadAllBytes(exe);
        Assert.All(ClientAddressPatch.Slots, lobby => Assert.Equal("192.0.2.10", ClientAddressPatch.ReadAddress(patched, ClientAddressPatch.FindSlot(patched, lobby))));
        var original = File.ReadAllBytes(exe + ClientAddressPatch.BackupSuffix);
        Assert.All(ClientAddressPatch.Slots, lobby => Assert.Equal(lobby.BuiltInAddress, ClientAddressPatch.ReadAddress(original, ClientAddressPatch.FindSlot(original, lobby))));
    }

    [Fact]
    public void AnotherLobbyPortIsExplained()
    {
        using var temp = new TempFolder();
        var exe = temp.Combine("Trickster.bin");
        File.WriteAllBytes(exe, FakeClient());

        var error = Assert.Throws<HostingException>(() => ClientAddressPatch.Apply(exe, "127.0.0.1", 19980));

        Assert.Contains("9980", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(exe + ClientAddressPatch.BackupSuffix));
    }

    [Fact]
    public void IPv6AddressesAreRefused()
    {
        Assert.Throws<HostingException>(() => ClientAddressPatch.ClientAddressFor("::1"));
        Assert.Equal("10.0.0.5", ClientAddressPatch.ClientAddressFor(" 10.0.0.5 "));
    }
}
