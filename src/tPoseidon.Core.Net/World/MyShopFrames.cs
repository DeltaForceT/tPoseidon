using System.Text;

namespace tPoseidon.Core.Net.World;

public readonly record struct MyShopBought(string Name, short PeriodHours);

public static class MyShopFrames
{
    public const int BoughtNameBytes = 33;

    public static byte[] EmptyList() => new byte[2];

    public static uint PackPeriod(short hours)
    {
        if (hours <= 0) return 0;
        return (uint)(((hours / 24) & 0xFF) << 8 | (hours % 24));
    }

    public static byte[] BoughtList(IReadOnlyList<MyShopBought> entries, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(encoding);
        var count = Math.Min(entries.Count, ushort.MaxValue);
        var w = new PacketWriter(2 + count * 48);
        w.WriteUInt16((ushort)count);
        for (var i = 0; i < count; i++)
        {
            var name = encoding.GetBytes(entries[i].Name);
            if (name.Length > BoughtNameBytes) name = name[..BoughtNameBytes];
            w.WriteBytes(name);
            w.WriteByte(0);
            w.WriteUInt32(PackPeriod(entries[i].PeriodHours));
            w.WriteUInt32(0);
            w.WriteByte(0);
            w.WriteUInt16(0);
        }

        return w.ToArray();
    }

    public static byte[] Entered(string text, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(text.Length + 1);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static byte[] Leave(ushort returnZone, ushort x, ushort y)
    {
        var w = new PacketWriter(6);
        w.WriteUInt16(returnZone);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        return w.ToArray();
    }
}
