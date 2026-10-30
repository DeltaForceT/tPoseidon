using System.Text;
using tPoseidon.Core.Game.ShadowWorld;

namespace tPoseidon.Core.Net.World;

public readonly record struct ShadowStatueGauge(
    int BarCountMinusOne,
    int FullBarsLeft,
    int BarCapacity,
    int CurrentBarHealth,
    int NearbyPlayers,
    int Attackers)
{
    public static ShadowStatueGauge Of(StatueState statue, int barCount, int nearbyPlayers, int attackers)
    {
        var bars = Math.Max(1, barCount);
        var capacity = Math.Max(1L, (statue.MaxHp + bars - 1) / bars);
        var current = Math.Max(0L, statue.CurrentHp);
        var full = current == 0 ? 0L : (current - 1) / capacity;
        var inBar = current - full * capacity;
        return new ShadowStatueGauge(bars - 1, (int)full, (int)capacity, (int)inBar, nearbyPlayers, attackers);
    }
}

public static class ShadowWorldFrames
{
    public const int StatueSlots = 5;
    public const int NameBytes = 32;
    public const byte MessagePresent = 1;

    public static byte[] StatueBoard(int remainingMilliseconds, IReadOnlyList<ShadowStatueGauge> statues)
    {
        var w = new PacketWriter(4 + StatueSlots * 24);
        WriteBoard(w, remainingMilliseconds, statues);
        return w.ToArray();
    }

    public static byte[] Bonus(int rewardId, long expiresAtUnixSeconds, long nowUnixSeconds)
    {
        var w = new PacketWriter(20);
        w.WriteInt32(rewardId);
        w.WriteUInt64((ulong)expiresAtUnixSeconds);
        w.WriteUInt64((ulong)nowUnixSeconds);
        return w.ToArray();
    }

    public const int ActivePhase = 0;
    public const int EndedPhase = 1;

    public static byte[] Phase(int phase)
    {
        var w = new PacketWriter(4);
        w.WriteInt32(phase);
        return w.ToArray();
    }

    public static byte[] LiveBoard(
        int remainingMilliseconds,
        IReadOnlyList<ShadowStatueGauge> statues,
        IReadOnlyList<ShadowStanding> standings,
        string? message,
        Encoding text)
    {
        ArgumentNullException.ThrowIfNull(standings);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(4 + StatueSlots * 24 + ShadowWorldMission.StandingSlots * 8 + 8);
        WriteBoard(w, remainingMilliseconds, statues);
        for (var slot = 0; slot < ShadowWorldMission.StandingSlots; slot++)
        {
            var standing = slot < standings.Count ? standings[slot] : default;
            w.WriteByte((byte)standing.Kind);
            if (standing.Kind != ShadowStandingKind.Empty) w.WriteNulTerminated(Clip(standing.Name, text), text);
        }
        w.WriteByte(message is null ? (byte)0 : MessagePresent);
        if (message is not null) w.WriteNulTerminated(message, text);
        return w.ToArray();
    }

    public static byte[] Result(ShadowResult result, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(16);
        w.WriteByte(result.Success ? (byte)1 : (byte)0);
        w.WriteUInt16((ushort)result.Outcome);
        w.WriteNulTerminated(Clip(result.WinnerName, text), text);
        return w.ToArray();
    }

    private static void WriteBoard(PacketWriter w, int remainingMilliseconds, IReadOnlyList<ShadowStatueGauge> statues)
    {
        ArgumentNullException.ThrowIfNull(statues);
        w.WriteInt32(remainingMilliseconds);
        for (var slot = 0; slot < StatueSlots; slot++)
        {
            var statue = slot < statues.Count ? statues[slot] : default;
            w.WriteInt32(statue.BarCountMinusOne);
            w.WriteInt32(statue.FullBarsLeft);
            w.WriteInt32(statue.BarCapacity);
            w.WriteInt32(statue.CurrentBarHealth);
            w.WriteInt32(statue.NearbyPlayers);
            w.WriteInt32(statue.Attackers);
        }
    }

    private static string Clip(string name, Encoding text)
    {
        var bytes = text.GetBytes(name);
        return bytes.Length < NameBytes ? name : text.GetString(bytes, 0, NameBytes - 1);
    }
}
