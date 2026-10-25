using System.Text;
using tPoseidon.Core.Game.PvP;

namespace tPoseidon.Core.Net.World;

public static class PvpDuelFrames
{
    public const int NameBytes = 32;
    public const int ParticipantBytes = 56;
    public const int ResultBytes = 54;
    public const ushort AnswerAccepted = 0;
    public const ushort AnswerRefused = 1;
    public const ushort AnswerBusy = 2;
    public const ushort ControlStart = 3;
    public const ushort ControlCancel = 4;

    public static bool TryReadName(ReadOnlySpan<byte> payload, Encoding text, out string name)
    {
        name = string.Empty;
        var end = payload.IndexOf((byte)0);
        if (end <= 0) return false;
        name = text.GetString(payload[..end]);
        return true;
    }

    public static bool TryReadNamedCode(ReadOnlySpan<byte> payload, Encoding text, out string name, out ushort code)
    {
        code = 0;
        if (!TryReadName(payload, text, out name)) return false;
        var rest = payload[(text.GetByteCount(name) + 1)..];
        if (rest.Length < sizeof(ushort)) return false;
        code = BitConverter.ToUInt16(rest[..sizeof(ushort)]);
        return true;
    }

    public static byte[] Participant(string name, uint level, PvpRecord record, Encoding text)
    {
        var w = new PacketWriter(ParticipantBytes);
        WriteParticipant(w, name, level, record, text);
        return w.ToArray();
    }

    public static byte[] Answer(ushort answer, byte[]? participant)
    {
        var w = new PacketWriter(2 + ParticipantBytes);
        w.WriteUInt16(answer);
        if (answer == AnswerAccepted && participant is not null) w.WriteBytes(participant);
        return w.ToArray();
    }

    public static byte[] Cancelled()
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(ControlCancel);
        return w.ToArray();
    }

    public static byte[] Prepare(uint prepareMs, string opponentName, byte pvpMode, Encoding text)
    {
        var w = new PacketWriter(8 + opponentName.Length * 2);
        w.WriteUInt32(prepareMs);
        w.WriteNulTerminated(opponentName, text);
        w.WriteByte(pvpMode);
        return w.ToArray();
    }

    public static byte[] Battle(uint battleMs)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(battleMs);
        return w.ToArray();
    }

    public static byte[] PrepareLost(string name, Encoding text)
    {
        var w = new PacketWriter(name.Length * 2 + 1);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] Result(PvpDuelSeatOutcome seat)
    {
        ArgumentNullException.ThrowIfNull(seat);
        var w = new PacketWriter(ResultBytes);
        w.WriteByte(seat.TimedOut ? (byte)1 : (byte)0);
        w.WriteByte(seat.Won ? (byte)1 : (byte)0);
        w.WriteUInt64(seat.CharacterId);
        w.WriteUInt32(seat.ZoneId);
        w.WriteUInt32(seat.Winner);
        w.WriteUInt32(seat.ElapsedMs);
        WriteRecord(w, seat.Record);
        w.WriteInt32(seat.LadderGain);
        w.WriteUInt32(seat.Exp);
        w.WriteUInt32(seat.ResultCode);
        return w.ToArray();
    }

    public static byte[] End(byte pvpMode) => new[] { pvpMode };

    private static void WriteParticipant(PacketWriter w, string name, uint level, PvpRecord record, Encoding text)
    {
        var encoded = text.GetBytes(name);
        if (encoded.Length >= NameBytes) throw new ArgumentException($"Name encodes to {encoded.Length} bytes; the field holds {NameBytes - 1}.", nameof(name));
        w.WriteBytes(encoded);
        w.WriteZero(NameBytes - encoded.Length);
        w.WriteUInt32(level);
        WriteRecord(w, record);
    }

    private static void WriteRecord(PacketWriter w, PvpRecord record)
    {
        w.WriteInt32(record.Ladder);
        w.WriteInt32(record.Matches);
        w.WriteInt32(record.Wins);
        w.WriteInt32(record.Losses);
        w.WriteInt32(record.Disconnects);
    }
}
