using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public sealed record SoulSeedUseRequest(int SeedItemIndex, ulong ItemUid, byte UpdateFlag);

public sealed record GuardianAwakened(ulong GuardianUid, byte SlotNum);

public static class SoulSeedMessages
{
    public const int PointCount = 8;

    public static byte[] EncodeUse(SoulSeedUseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var w = new PacketWriter(13);
        w.WriteInt32(request.SeedItemIndex);
        w.WriteUInt64(request.ItemUid);
        w.WriteByte(request.UpdateFlag);
        return w.ToArray();
    }

    public static SoulSeedUseRequest DecodeUse(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new SoulSeedUseRequest(r.ReadInt32(), r.ReadUInt64(), r.ReadByte());
    }

    public static byte[] EncodeUid(ulong uid)
    {
        var w = new PacketWriter(sizeof(ulong));
        w.WriteUInt64(uid);
        return w.ToArray();
    }

    public static ulong DecodeUid(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt64();

    public static byte[] EncodeCode(uint code)
    {
        var w = new PacketWriter(sizeof(uint));
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static uint DecodeCode(ReadOnlySpan<byte> body) => body.Length < sizeof(uint) ? 0 : new PacketReader(body).ReadUInt32();

    public static byte[] EncodePoints(IReadOnlyList<short> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        var w = new PacketWriter(PointCount * sizeof(short));
        for (var i = 0; i < PointCount; i++) w.WriteInt16(i < points.Count ? points[i] : (short)0);
        return w.ToArray();
    }

    public static IReadOnlyList<short> DecodePoints(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var points = new short[PointCount];
        for (var i = 0; i < PointCount; i++) points[i] = r.ReadInt16();
        return points;
    }

    public static byte[] EncodeState(SoulSeedState? state)
    {
        var w = new PacketWriter(40);
        if (state is null)
        {
            w.WriteByte(0);
            return w.ToArray();
        }
        w.WriteByte(1);
        w.WriteInt32(state.SeedItemIndex);
        w.WriteInt64(state.StartAt.ToUnixTimeSeconds());
        w.WriteInt64(state.LastUpdateAt.ToUnixTimeSeconds());
        w.WriteBytes(EncodePoints(state.Points));
        return w.ToArray();
    }

    public static SoulSeedState? DecodeState(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        if (r.ReadByte() == 0) return null;
        var seed = r.ReadInt32();
        var start = DateTimeOffset.FromUnixTimeSeconds(r.ReadInt64());
        var last = DateTimeOffset.FromUnixTimeSeconds(r.ReadInt64());
        var points = new short[PointCount];
        for (var i = 0; i < PointCount; i++) points[i] = r.ReadInt16();
        return new SoulSeedState(seed, start, last, points);
    }

    public static byte[] EncodeAwaken(GuardianAwakening awakening)
    {
        ArgumentNullException.ThrowIfNull(awakening);
        var w = new PacketWriter(64);
        w.WriteString(awakening.Name);
        w.WriteInt16(awakening.GuardianType);
        w.WriteByte(awakening.FType);
        w.WriteByte(awakening.PtsP);
        w.WriteByte(awakening.PtsM);
        w.WriteByte(awakening.PtsS);
        w.WriteByte(awakening.PtsC);
        w.WriteByte(awakening.RareType);
        w.WriteByte(awakening.TitleIndex);
        w.WriteInt32(awakening.SummonSkillId);
        return w.ToArray();
    }

    public static GuardianAwakening DecodeAwaken(ulong characterId, ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var name = r.ReadString();
        var type = r.ReadInt16();
        var fType = r.ReadByte();
        var p = r.ReadByte();
        var m = r.ReadByte();
        var s = r.ReadByte();
        var c = r.ReadByte();
        var rare = r.ReadByte();
        var title = r.ReadByte();
        return new GuardianAwakening(characterId, name, type, fType, rare, title, p, m, s, c, r.ReadInt32());
    }

    public static byte[] EncodeReset(ushort skillId)
    {
        var w = new PacketWriter(sizeof(ushort));
        w.WriteUInt16(skillId);
        return w.ToArray();
    }

    public static ushort DecodeReset(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt16();

    public static byte[] EncodeAwakened(GuardianAwakened awakened)
    {
        ArgumentNullException.ThrowIfNull(awakened);
        var w = new PacketWriter(9);
        w.WriteUInt64(awakened.GuardianUid);
        w.WriteByte(awakened.SlotNum);
        return w.ToArray();
    }

    public static GuardianAwakened DecodeAwakened(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return new GuardianAwakened(r.ReadUInt64(), r.ReadByte());
    }
}
