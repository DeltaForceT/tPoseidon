using System.Globalization;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record GiftAcceptRequest(uint GiftCode, string ReceiveMessage, string SenderName);

public static class GiftFrames
{
    public const ushort SessionOpen = 0x2AF9;
    public const ushort Accept = 0x2AFB;
    public const ushort Accepted = 0x2AFC;
    public const ushort ReceivedRequest = 0x2AFD;
    public const ushort ReceivedList = 0x2AFE;
    public const ushort RemainCount = 0x2AFF;
    public const ushort PendingList = 0x2B00;
    public const ushort SentList = 0x2B01;
    public const ushort RemainCountRequest = 0x2B02;
    public const ushort PendingRequest = 0x2B03;
    public const ushort SentRequest = 0x2B04;

    public const string DateFormat = "yyyy-MM-dd HH:mm:ss";
    public const int NameBytes = 0x20 - 1;
    public const int DateBytes = 0x20 - 1;
    public const int MessageBytes = 0x80 - 1;
    public const uint ClientRowExtension = 0;

    public static bool TryReadAccept(ReadOnlySpan<byte> payload, Encoding text, out GiftAcceptRequest request)
    {
        ArgumentNullException.ThrowIfNull(text);
        request = new GiftAcceptRequest(0, string.Empty, string.Empty);
        if (payload.Length < 6) return false;
        try
        {
            var r = new PacketReader(payload);
            var giftCode = r.ReadUInt32();
            var message = r.ReadNulTerminated(text);
            request = new GiftAcceptRequest(giftCode, message, r.ReadNulTerminated(text));
            return true;
        }
        catch (InvalidPacketException)
        {
            return false;
        }
    }

    public static byte[] Received(IReadOnlyList<GiftReceivedRow> rows, Encoding text) =>
        List(rows, text, (w, row) =>
        {
            w.WriteUInt32(row.GoodsCode);
            w.WriteNulTerminated(Clip(Date(row.TakeDate), DateBytes, text), text);
            w.WriteNulTerminated(Clip(row.SenderName, NameBytes, text), text);
            w.WriteNulTerminated(Clip(row.GiftMessage, MessageBytes, text), text);
            w.WriteUInt32(unchecked((uint)row.SenderType));
            w.WriteUInt32(ClientRowExtension);
        });

    public static byte[] Pending(IReadOnlyList<GiftPendingRow> rows, Encoding text) =>
        List(rows, text, (w, row) =>
        {
            w.WriteUInt32(row.GiftCode);
            w.WriteUInt32(row.GoodsCode);
            w.WriteNulTerminated(Clip(row.SenderName, NameBytes, text), text);
            w.WriteNulTerminated(Clip(Date(row.GiftDate), DateBytes, text), text);
            w.WriteNulTerminated(Clip(row.GiftMessage, MessageBytes, text), text);
            w.WriteUInt32(unchecked((uint)row.SenderType));
            w.WriteUInt32(ClientRowExtension);
        });

    public static byte[] Sent(IReadOnlyList<GiftSentRow> rows, Encoding text) =>
        List(rows, text, (w, row) =>
        {
            w.WriteUInt32(row.GoodsCode);
            w.WriteNulTerminated(Clip(Date(row.GiftDate), DateBytes, text), text);
            w.WriteNulTerminated(Clip(row.ReceiverName, NameBytes, text), text);
            w.WriteNulTerminated(Clip(row.ReceiveMessage, MessageBytes, text), text);
            w.WriteUInt32(row.Taken ? 1u : 0u);
            w.WriteUInt32(unchecked((uint)row.ReceiverType));
            w.WriteUInt32(ClientRowExtension);
        });

    public static byte[] Remain(int count)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32((uint)count);
        return w.ToArray();
    }

    public static readonly TimeSpan SmallDateTimeRoundUp = TimeSpan.FromMilliseconds(29999);

    public static string Date(DateTimeOffset at)
    {
        var local = at.ToLocalTime().DateTime;
        var minute = new DateTime(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, local.Kind);
        if (local - minute >= SmallDateTimeRoundUp) minute = minute.AddMinutes(1);
        return minute.ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    private static string Clip(string value, int maxBytes, Encoding text)
    {
        if (text.GetByteCount(value) <= maxBytes) return value;
        var length = value.Length;
        while (length > 0 && text.GetByteCount(value.AsSpan(0, length)) > maxBytes) length--;
        return value[..length];
    }

    private static byte[] List<T>(IReadOnlyList<T> rows, Encoding text, Action<PacketWriter, T> write)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(text);
        var shown = rows.Take(GiftItemRules.ListRowLimit).ToArray();
        var w = new PacketWriter(8 + shown.Length * 96);
        w.WriteUInt32(rows.Count > GiftItemRules.ListRowLimit ? 1u : 0u);
        w.WriteUInt32((uint)shown.Length);
        foreach (var row in shown) write(w, row);
        return w.ToArray();
    }

    public static byte[] AcceptResult(uint success)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(success);
        return w.ToArray();
    }
}
