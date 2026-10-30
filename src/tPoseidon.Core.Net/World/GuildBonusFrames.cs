using tPoseidon.Core.Game.Guild;

namespace tPoseidon.Core.Net.World;

public static class GuildBonusFrames
{
    public const int RankBonusSize = sizeof(int) + sizeof(float);

    public static byte[] RankBonus(GuildBonusStanding standing)
    {
        var w = new PacketWriter(RankBonusSize);
        w.WriteInt32(standing.Rank);
        w.WriteUInt32(BitConverter.SingleToUInt32Bits(standing.Ratio));
        return w.ToArray();
    }
}
