using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Game.Angel;

namespace tPoseidon.Core.Net.Packets;

[WireOpcode((ushort)Opcode.S2C_OP_2E29_TBD)]
public sealed class AngelWingPacket
{
    public const int PayloadSize = 3;

    public bool WingActive { get; init; }
    public ushort GraduationCount { get; init; }

    public static AngelWingPacket Of(AngelResult? result, DateTimeOffset now) => new()
    {
        WingActive = result is not null && AngelRules.WingActive(result.LatestGraduation, now),
        GraduationCount = (ushort)Math.Clamp(result?.GraduationCount ?? 0, 0, ushort.MaxValue),
    };

    public byte[] EncodePayload()
    {
        var buf = new byte[PayloadSize];
        buf[0] = WingActive ? (byte)1 : (byte)0;
        buf[1] = (byte)(GraduationCount & 0xFF);
        buf[2] = (byte)((GraduationCount >> 8) & 0xFF);
        return buf;
    }
}
