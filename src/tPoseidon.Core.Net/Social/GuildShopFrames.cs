using System.Buffers.Binary;
using System.Text;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public static class GuildShopFrames
{
    public const ushort Buy = (ushort)Opcode.C2S_GUILD_SHOP_BUY;
    public const ushort Bought = (ushort)Opcode.S2C_GUILD_SHOP_BOUGHT;
    public const ushort Error = (ushort)Opcode.S2C_GUILD_SHOP_ERROR;
    public const ushort HistoryRequest = (ushort)Opcode.C2S_GUILD_SHOP_HISTORY;
    public const ushort HistoryResult = (ushort)Opcode.S2C_GUILD_SHOP_HISTORY;
    public const ushort MaxMembers = (ushort)Opcode.S2C_GUILD_MAX_MEMBERS;

    public const int BuyRequestLength = 4;
    public const int HistoryNameBytes = 20;
    public const int HistoryEntryLength = 0x24;

    public static uint? ReadBuy(ReadOnlySpan<byte> payload) =>
        payload.Length < BuyRequestLength ? null : BinaryPrimitives.ReadUInt32LittleEndian(payload);

    public static byte[] BoughtFor(uint goodsIndex) => Word(goodsIndex);

    public static byte[] ErrorFor(int code) => Word((uint)code);

    public static byte[] MaxMembersFor(int maxMembers) => Word((uint)maxMembers);

    public static byte[] HistoryFor(IReadOnlyList<GuildShopHistoryEntry> entries, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(sizeof(uint) + entries.Count * HistoryEntryLength);
        w.WriteUInt32((uint)entries.Count);
        foreach (var entry in entries)
        {
            w.WriteUInt64((ulong)entry.At.ToFileTime());
            var name = FittedName(entry.CharacterName, text);
            w.WriteBytes(name);
            w.WriteZero(HistoryNameBytes - name.Length);
            w.WriteUInt32((uint)entry.GoodsIndex);
            w.WriteUInt32((uint)entry.Coins);
        }
        return w.ToArray();
    }

    private static byte[] Word(uint value)
    {
        var payload = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, value);
        return payload;
    }

    private static byte[] FittedName(string name, Encoding text)
    {
        var length = name.Length;
        while (length > 0 && text.GetByteCount(name.AsSpan(0, length)) > HistoryNameBytes - 1) length--;
        return text.GetBytes(name[..length]);
    }
}
