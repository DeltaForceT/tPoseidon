using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class FortuneMessages
{
    public static byte[] EncodeInfo(FortuneInfo? info)
    {
        var w = new PacketWriter(64);
        if (info is null)
        {
            w.WriteByte(0);
            return w.ToArray();
        }
        w.WriteByte(1);
        w.WriteInt32(info.TitleId);
        w.WriteInt32(info.ViewCount);
        w.WriteInt64(info.UpdatedAt.ToUnixTimeSeconds());
        w.WriteInt64(info.ExpiresAt.ToUnixTimeSeconds());
        w.WriteString(info.ItemAttrString);
        return w.ToArray();
    }

    public static FortuneInfo? DecodeInfo(ulong characterId, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        if (r.ReadByte() == 0) return null;
        return new FortuneInfo
        {
            CharacterId = characterId,
            TitleId = r.ReadInt32(),
            ViewCount = r.ReadInt32(),
            UpdatedAt = DateTimeOffset.FromUnixTimeSeconds(r.ReadInt64()),
            ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(r.ReadInt64()),
            ItemAttrString = r.ReadString(),
        };
    }
}
