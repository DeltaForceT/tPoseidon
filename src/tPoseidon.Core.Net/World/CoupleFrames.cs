using System.Text;
using tPoseidon.Core.Game.Wedding;
using tPoseidon.Core.LibConfig;

namespace tPoseidon.Core.Net.World;

public static class CoupleFrames
{
    public const int OptionRequestSize = sizeof(ulong) + sizeof(uint) + 2 * sizeof(ushort);
    public const int MaxCouponOperationSize = 24;

    public static byte[] SkillLearned(ushort ringItemIndex)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(ringItemIndex);
        return w.ToArray();
    }

    public static byte[] MakeEnded(uint code)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static bool TryReadTarget(ReadOnlySpan<byte> payload, out ushort targetEntityId)
    {
        targetEntityId = 0;
        if (payload.Length < sizeof(ushort)) return false;
        targetEntityId = new PacketReader(payload).ReadUInt16();
        return true;
    }

    public static bool TryReadReady(ReadOnlySpan<byte> payload, out byte ready)
    {
        ready = 0;
        if (payload.Length < 1) return false;
        ready = payload[0];
        return true;
    }

    public static bool TryReadOption(ReadOnlySpan<byte> payload, out MarriageOptionRequest request)
    {
        request = default;
        if (payload.Length < OptionRequestSize) return false;
        var r = new PacketReader(payload);
        request = new MarriageOptionRequest(r.ReadUInt64(), (int)r.ReadUInt32(), r.ReadUInt16(), r.ReadUInt16());
        return true;
    }

    public static byte[] OptionDone(BagOperation? ring, BagOperation? dress)
    {
        var w = new PacketWriter(2 * (sizeof(byte) + MaxCouponOperationSize));
        WriteCoupon(w, ring);
        WriteCoupon(w, dress);
        return w.ToArray();
    }

    private static void WriteCoupon(PacketWriter w, BagOperation? coupon)
    {
        if (coupon is not { } operation)
        {
            w.WriteByte(0);
            return;
        }
        w.WriteByte(1);
        w.WriteBytes(QuestResultFrames.ItemOperation(operation));
    }

    public static byte[] OptionRefused(uint code)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static byte[] Proposed(byte role, string otherName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(24);
        w.WriteByte(role);
        w.WriteNulTerminated(otherName, text);
        return w.ToArray();
    }

    public static byte[] MakeState(byte flag, byte recipientReady, string partnerName, ushort partnerType, byte partnerReady, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(28);
        w.WriteByte(flag);
        w.WriteByte(recipientReady);
        w.WriteNulTerminated(partnerName, text);
        w.WriteUInt16(partnerType);
        w.WriteByte(partnerReady);
        return w.ToArray();
    }

    public static byte[] PartnerName(string partnerName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(24);
        w.WriteNulTerminated(partnerName, text);
        return w.ToArray();
    }

    public static byte[] ExchangeList(IReadOnlyList<MarriageExchangeEntry> rows, IReadOnlyList<MarriageTitleEntry> titles, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(titles);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(8 + rows.Count * 32 + titles.Count * 4);
        w.WriteUInt32((uint)rows.Count);
        foreach (var row in rows)
        {
            w.WriteUInt16((ushort)row.ResId);
            w.WriteUInt32((uint)row.Category);
            w.WriteNulTerminated(row.Name, text);
            w.WriteUInt32((uint)row.Fee);
        }
        w.WriteUInt32((uint)titles.Count);
        foreach (var title in titles) w.WriteUInt32((uint)title.Index);
        return w.ToArray();
    }

    public static byte[] Stage(uint stage)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(stage);
        return w.ToArray();
    }

    public static byte[] Joined(bool coupleMember) => new[] { coupleMember ? (byte)1 : (byte)0 };

    public static byte[] Scene(uint step, string groomName, string brideName, string groomSay, string brideSay, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteUInt32(step);
        switch (step)
        {
            case WeddingScene.StartMessage:
            case WeddingScene.SceneEnd:
                w.WriteNulTerminated(groomName, text);
                w.WriteNulTerminated(brideName, text);
                break;
            case WeddingScene.GroomStartMessage:
                w.WriteNulTerminated(groomName, text);
                break;
            case WeddingScene.BrideStartMessage:
                w.WriteNulTerminated(brideName, text);
                break;
            case WeddingScene.GroomSay:
                w.WriteNulTerminated(groomSay, text);
                break;
            case WeddingScene.BrideSay:
                w.WriteNulTerminated(brideSay, text);
                break;
        }
        return w.ToArray();
    }
}
