using System.Text;

namespace tPoseidon.Hosting.Tests;

public sealed class ClientAddressPatchTests
{
    private const int MarkerOffset = 0x40;

    private static byte[] FakeClient(string address = ClientAddressPatch.BuiltInLobbyAddress)
    {
        var bytes = new byte[0x80];
        bytes.AsSpan().Fill(0xCC);
        Slot(bytes, MarkerOffset).Clear();
        Encoding.ASCII.GetBytes(ClientAddressPatch.PrecedingAddress).CopyTo(bytes, MarkerOffset);
        Slot(bytes, MarkerOffset + ClientAddressPatch.SlotBytes).Clear();
        Encoding.ASCII.GetBytes(address).CopyTo(bytes, MarkerOffset + ClientAddressPatch.SlotBytes);
        return bytes;
    }

    private static Span<byte> Slot(byte[] bytes, int offset) => bytes.AsSpan(offset, ClientAddressPatch.SlotBytes);

    [Fact]
    public void TheSlotFollowsTheTestServerAddress()
    {
        var client = FakeClient();
        var slot = ClientAddressPatch.FindSlot(client);

        Assert.Equal(MarkerOffset + ClientAddressPatch.SlotBytes, slot);
        Assert.Equal(ClientAddressPatch.BuiltInLobbyAddress, ClientAddressPatch.ReadAddress(client, slot));
    }

    [Fact]
    public void WritingAnAddressClearsTheRestOfTheSlot()
    {
        var client = FakeClient();
        var slot = ClientAddressPatch.FindSlot(client);

        ClientAddressPatch.WriteAddress(client, slot, "127.0.0.1");

        Assert.Equal("127.0.0.1", ClientAddressPatch.ReadAddress(client, slot));
        Assert.All(client.AsSpan(slot + 9, ClientAddressPatch.SlotBytes - 9).ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(0xCC, client[slot + ClientAddressPatch.SlotBytes]);
        Assert.Equal(slot, ClientAddressPatch.FindSlot(client));
    }

    [Fact]
    public void ALongestAddressFitsAndOtherTextIsRefused()
    {
        var client = FakeClient();
        var slot = ClientAddressPatch.FindSlot(client);

        ClientAddressPatch.WriteAddress(client, slot, "255.255.255.255");
        Assert.Equal("255.255.255.255", ClientAddressPatch.ReadAddress(client, slot));
        Assert.Throws<ArgumentException>(() => ClientAddressPatch.WriteAddress(client, slot, "play.example.org"));
    }

    [Fact]
    public void AClientWithoutTheMarkerIsNotTouched()
    {
        var bytes = new byte[0x80];
        Assert.Equal(-1, ClientAddressPatch.FindSlot(bytes));
    }

    [Fact]
    public void ApplyPatchesOnceKeepsTheOriginalAndRepatchesInPlace()
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
        Assert.Equal("192.0.2.10", ClientAddressPatch.ReadAddress(patched, ClientAddressPatch.FindSlot(patched)));
        var original = File.ReadAllBytes(exe + ClientAddressPatch.BackupSuffix);
        Assert.Equal(ClientAddressPatch.BuiltInLobbyAddress, ClientAddressPatch.ReadAddress(original, ClientAddressPatch.FindSlot(original)));
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
