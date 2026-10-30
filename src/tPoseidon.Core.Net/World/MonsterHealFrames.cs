using tPoseidon.Core.Game.Monsters;

namespace tPoseidon.Core.Net.World;

public static class MonsterHealFrames
{
    public const int VampiricSize = sizeof(ushort) + sizeof(int) + sizeof(ushort) + sizeof(byte) + sizeof(int);

    public static byte[] Vampiric(VampiricHealNotice notice)
    {
        var w = new PacketWriter(VampiricSize);
        w.WriteUInt16(notice.MonsterEntityId);
        w.WriteInt32(notice.Hp);
        w.WriteUInt16(notice.HpBars);
        w.WriteByte(notice.BossFlag ? (byte)1 : (byte)0);
        w.WriteInt32(notice.Healed);
        return w.ToArray();
    }
}
