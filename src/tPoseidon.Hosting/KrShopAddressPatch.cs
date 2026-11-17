using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Hosting;

public enum ShopPatchOutcome
{
    Patched,
    BadAddress,
    UnknownClient,
}

public readonly record struct ShopPatchResult(ShopPatchOutcome Outcome, string Message)
{
    public bool Succeeded => Outcome == ShopPatchOutcome.Patched;
}

public static class KrShopAddressPatch
{
    public const int MaxBaseLength = 42;
    public const int SlotSize = 64;
    public const string BalancePage = "mypoint.aspx";
    public const string AgreePage = "agree_charge_cli.aspx";

    private const int BaseSlot = 0xB49C50;
    private const int BalanceSlot = 0xB49C90;
    private const int AgreeSlot = 0xB49CD0;
    private const uint BaseSlotVa = 0x00F4B050;
    private const uint BalanceSlotVa = 0x00F4B090;
    private const uint AgreeSlotVa = 0x00F4B0D0;
    private const uint ChargePageVa = 0x00DAFAC0;
    private const int RdataVirtualSizeOffset = 0x260;
    private const uint RdataVirtualSize = 0x001A104E;
    private const uint RdataRawSize = 0x001A1200;

    private static readonly (int Offset, uint Original, uint Patched)[] Pointers =
    [
        (0x1AF3A, 0x00DAFA78, BaseSlotVa),
        (0x1AF30, 0x00DAFA90, BalanceSlotVa),
        (0x1AF4E, 0x00DAFA08, AgreeSlotVa),
        (0x1AF44, 0x00DAFA4C, ChargePageVa),
        (0x1AF11, 0x00DAFB20, BaseSlotVa),
        (0x1AF07, 0x00DAFB3C, BalanceSlotVa),
        (0x1AF1B, 0x00DAFAD8, AgreeSlotVa),
        (0x1B015, 0x00DAF6F0, BaseSlotVa),
        (0x1B00B, 0x00DAF71C, BalanceSlotVa),
        (0x1B01F, 0x00DAF6B0, AgreeSlotVa),
    ];

    public static string BaseUrl(string host, int port) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"http://{host}:{port}/");

    public static string? CheckBaseUrl(string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        if (baseUrl.Length > MaxBaseLength) return $"The shop address {baseUrl} is longer than {MaxBaseLength} characters.";
        if (!baseUrl.StartsWith("http://", StringComparison.Ordinal) || !baseUrl.EndsWith('/')) return $"The shop address {baseUrl} must start with http:// and end with /.";
        if (baseUrl.Any(c => c < 0x21 || c > 0x7E)) return $"The shop address {baseUrl} must be plain ASCII without spaces.";
        return Uri.TryCreate(baseUrl, UriKind.Absolute, out _) ? null : $"The shop address {baseUrl} is not a valid address.";
    }

    public static ShopPatchResult Apply(byte[] image, string baseUrl)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (CheckBaseUrl(baseUrl) is { } bad) return new ShopPatchResult(ShopPatchOutcome.BadAddress, bad);
        if (image.Length < AgreeSlot + SlotSize) return Unknown("is too small");
        var virtualSize = Read(image, RdataVirtualSizeOffset);
        if (virtualSize != RdataVirtualSize && virtualSize != RdataRawSize) return Unknown("has a different section layout");
        foreach (var (offset, original, patched) in Pointers)
        {
            var value = Read(image, offset);
            if (value != original && value != patched) return Unknown($"has unexpected bytes at 0x{offset:X}");
        }
        var alreadyPatched = Read(image, Pointers[0].Offset) == Pointers[0].Patched;
        if (!alreadyPatched && image.AsSpan(BaseSlot, AgreeSlot + SlotSize - BaseSlot).ContainsAnyExcept((byte)0)) return Unknown("has data where the shop address goes");

        WriteSlot(image, BaseSlot, baseUrl);
        WriteSlot(image, BalanceSlot, baseUrl + BalancePage);
        WriteSlot(image, AgreeSlot, baseUrl + AgreePage);
        foreach (var (offset, _, patched) in Pointers) BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(offset, sizeof(uint)), patched);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(RdataVirtualSizeOffset, sizeof(uint)), RdataRawSize);
        return new ShopPatchResult(ShopPatchOutcome.Patched, $"The client shop pages now open from {baseUrl}.");
    }

    public static string? PatchedBaseUrl(byte[] image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (image.Length < AgreeSlot + SlotSize || Read(image, Pointers[0].Offset) != BaseSlotVa) return null;
        var slot = image.AsSpan(BaseSlot, SlotSize);
        var end = slot.IndexOf((byte)0);
        return end <= 0 ? null : Encoding.ASCII.GetString(slot[..end]);
    }

    private static void WriteSlot(byte[] image, int offset, string text)
    {
        var slot = image.AsSpan(offset, SlotSize);
        slot.Clear();
        Encoding.ASCII.GetBytes(text, slot);
    }

    private static uint Read(byte[] image, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset, sizeof(uint)));

    private static ShopPatchResult Unknown(string reason) =>
        new(ShopPatchOutcome.UnknownClient, $"This KR client {reason}; the shop address patch only fits the 2014.01.28 build.");
}
