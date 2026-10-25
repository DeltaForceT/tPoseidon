using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record AngelCall(byte Type, ulong AngelCharacterId, string Message);

public sealed record AngelSpot(ulong AngelCharacterId, uint Zone, short X, short Y);

public static class AngelFrames
{
    public const ushort SessionOpen = 0x2E19;
    public const ushort ListRequest = 0x2E1B;
    public const ushort List = 0x2E1C;
    public const ushort Call = 0x2E1D;
    public const ushort AngelComing = 0x2E1E;
    public const ushort Refused = 0x2E1F;
    public const ushort Listed = 0x2E20;
    public const ushort RemoveListed = 0x2E21;
    public const ushort CallToAngel = 0x2E24;
    public const ushort Dismiss = 0x2E25;
    public const ushort Accept = 0x2E26;
    public const ushort AngelMoving = 0x2E27;
    public const ushort AcceptRefused = 0x2E28;
    public const ushort Portal = 0x2E2C;
    public const ushort Busy = 0x2E2D;
    public const ushort GraduationMemo = 0x2181;
    public const ushort NewbieGraduated = 0x2E23;
    public const string GraduationMemoKey = "_GRADUATE";

    public const ushort OrderNewbiePosition = 0xDC;
    public const ushort OrderAngelMove = 0xDD;
    public const ushort OrderAngelReward = 0xDE;

    public const int NameField = 24;
    public const int SpotSize = sizeof(ulong) + sizeof(uint) + 2 * sizeof(short);

    public static byte[] GraduationMemoFor(string newbieName, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(newbieName);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteNulTerminated(newbieName, text);
        w.WriteNulTerminated(GraduationMemoKey, text);
        return w.ToArray();
    }

    public static byte[] ListOf(IReadOnlyList<AngelRelation> angels, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(angels);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(4 + angels.Count * 32);
        w.WriteUInt32((uint)angels.Count);
        foreach (var angel in angels)
        {
            w.WriteUInt64(angel.AngelCharacterId);
            var name = text.GetBytes(angel.Name);
            var kept = Math.Min(name.Length, NameField - 1);
            w.WriteBytes(name.AsSpan(0, kept));
            w.WriteZero(NameField - kept);
        }
        return w.ToArray();
    }

    public static AngelCall? ReadCall(ReadOnlySpan<byte> payload, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (payload.Length < 1) return null;
        var r = new PacketReader(payload);
        var type = r.ReadByte();
        ulong angel = 0;
        if (type == tPoseidon.Core.Game.Angel.AngelRules.RequestListed)
        {
            if (payload.Length < 9) return null;
            angel = r.ReadUInt64();
        }
        var consumed = type == tPoseidon.Core.Game.Angel.AngelRules.RequestListed ? 9 : 1;
        var rest = payload[consumed..];
        var end = rest.IndexOf((byte)0);
        var message = text.GetString(end < 0 ? rest : rest[..end]);
        return new AngelCall(type, angel, message);
    }

    public static byte[] CallFor(string newbieName, ushort level, ushort zone, bool listed, string message, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteNulTerminated(newbieName, text);
        w.WriteUInt16(level);
        w.WriteUInt16(zone);
        w.WriteByte(listed ? (byte)1 : (byte)0);
        w.WriteNulTerminated(message, text);
        return w.ToArray();
    }

    public static byte[] Code(byte code) => new[] { code };

    public static byte[] RudeWait(TimeSpan left)
    {
        var w = new PacketWriter(11);
        w.WriteByte(tPoseidon.Core.Game.Angel.AngelRules.RudeNewbie);
        w.WriteUInt16((ushort)Math.Min(ushort.MaxValue, Math.Ceiling(left.TotalSeconds)));
        w.WriteUInt64(0);
        return w.ToArray();
    }

    public static bool TryReadAccept(ReadOnlySpan<byte> payload, out ushort zone, out ushort x, out ushort y)
    {
        zone = x = y = 0;
        if (payload.Length < 6) return false;
        var r = new PacketReader(payload);
        zone = r.ReadUInt16();
        x = r.ReadUInt16();
        y = r.ReadUInt16();
        return true;
    }

    public static bool TryReadPortal(ReadOnlySpan<byte> payload, out bool goBack, out bool rude)
    {
        goBack = rude = false;
        if (payload.Length < 2) return false;
        goBack = payload[0] == 1;
        rude = payload[1] != 0;
        return true;
    }

    public static bool TryReadFlag(ReadOnlySpan<byte> payload, out bool flag)
    {
        flag = false;
        if (payload.Length < 1) return false;
        flag = payload[0] != 0;
        return true;
    }

    public static bool TryReadCharacter(ReadOnlySpan<byte> payload, out ulong characterId)
    {
        characterId = 0;
        if (payload.Length < 8) return false;
        characterId = new PacketReader(payload).ReadUInt64();
        return true;
    }

    public static byte[] Character(ulong characterId)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static byte[] Pair(ulong first, ulong second)
    {
        var w = new PacketWriter(16);
        w.WriteUInt64(first);
        w.WriteUInt64(second);
        return w.ToArray();
    }

    public static bool TryReadPair(ReadOnlySpan<byte> payload, out ulong first, out ulong second)
    {
        first = second = 0;
        if (payload.Length < 16) return false;
        var r = new PacketReader(payload);
        first = r.ReadUInt64();
        second = r.ReadUInt64();
        return true;
    }

    public static byte[] Spot(AngelSpot spot)
    {
        ArgumentNullException.ThrowIfNull(spot);
        var w = new PacketWriter(SpotSize);
        w.WriteUInt64(spot.AngelCharacterId);
        w.WriteUInt32(spot.Zone);
        w.WriteInt16(spot.X);
        w.WriteInt16(spot.Y);
        return w.ToArray();
    }

    public static AngelSpot? ReadSpot(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < SpotSize) return null;
        var r = new PacketReader(payload);
        return new AngelSpot(r.ReadUInt64(), r.ReadUInt32(), r.ReadInt16(), r.ReadInt16());
    }

    public static byte[] Named(string name, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] ListedAngel(ulong angelCharacterId, string name, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(32);
        w.WriteUInt64(angelCharacterId);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] ReturnMessage(ulong angelCharacterId, ushort x, ushort y, ushort zone)
    {
        var w = new PacketWriter(14);
        w.WriteUInt64(angelCharacterId);
        w.WriteUInt16(x);
        w.WriteUInt16(y);
        w.WriteUInt16(zone);
        return w.ToArray();
    }

    public static bool TryReadReturnMessage(ReadOnlySpan<byte> payload, out ulong angelCharacterId, out ushort x, out ushort y, out ushort zone)
    {
        angelCharacterId = 0;
        x = y = zone = 0;
        if (payload.Length < 14) return false;
        var r = new PacketReader(payload);
        angelCharacterId = r.ReadUInt64();
        x = r.ReadUInt16();
        y = r.ReadUInt16();
        zone = r.ReadUInt16();
        return true;
    }
}
