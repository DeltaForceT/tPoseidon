namespace tPoseidon.Core.Net.World;

public static class ProgressFrames
{
    public const int ExpGainedSize = 4;
    public const int TmTotalSize = 5;
    public const int TmLevelUpSize = 9;
    public const int LevelSeenSize = 4;
    public const int OwnerTakenSize = 4;
    public const int OwnerReleasedSize = 2;
    public const int SpeedSize = 3;
    public const int StatPointsSize = 26;

    public const byte TmFromMonsterKill = 0;
    public const int LevelUpSize = CharacterStatBlock.Size + 13;
    public const byte GeneralGuideList = 0;

    public const double SpeedUnit = tPoseidon.Core.Game.Monsters.ClientSpeed.Unit;

    public static byte[] ExpGained(long gain)
    {
        var w = new PacketWriter(ExpGainedSize);
        w.WriteUInt32Field(gain);
        return w.ToArray();
    }

    public static byte[] LevelUp(CharacterStatBlock stats, long hp, long mp, ulong exp)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var w = new PacketWriter(LevelUpSize);
        stats.WriteTo(w);
        w.WriteUInt32Field(hp);
        w.WriteUInt32Field(mp);
        w.WriteUInt32Field(exp);
        w.WriteByte(GeneralGuideList);
        return w.ToArray();
    }

    public static byte[] TmTotal(byte source, long tmExp)
    {
        var w = new PacketWriter(TmTotalSize);
        w.WriteByte(source);
        w.WriteUInt32Field(tmExp);
        return w.ToArray();
    }

    public static byte[] TmLevelUp(byte characterType, ushort tmLevel, long tmExp, ushort tmPoint)
    {
        var w = new PacketWriter(TmLevelUpSize);
        w.WriteByte(characterType);
        w.WriteUInt16(tmLevel);
        w.WriteUInt32Field(tmExp);
        w.WriteUInt16(tmPoint);
        return w.ToArray();
    }

    public static byte[] LevelSeen(ushort playerId, ushort level)
    {
        var w = new PacketWriter(LevelSeenSize);
        w.WriteUInt16(playerId);
        w.WriteUInt16(level);
        return w.ToArray();
    }

    public static byte[] StatPoints(ushort bonusPoints, IReadOnlyList<short> statPoints)
    {
        ArgumentNullException.ThrowIfNull(statPoints);
        if (statPoints.Count != CharacterStatBlock.StatCount)
        {
            throw new ArgumentException($"A character has {CharacterStatBlock.StatCount} stats, got {statPoints.Count}.", nameof(statPoints));
        }
        var w = new PacketWriter(StatPointsSize);
        w.WriteUInt16(bonusPoints);
        foreach (var points in statPoints) w.WriteInt16(points);
        return w.ToArray();
    }

    public static byte[] OwnerTaken(ushort monsterId, ushort ownerId)
    {
        var w = new PacketWriter(OwnerTakenSize);
        w.WriteUInt16(monsterId);
        w.WriteUInt16(ownerId);
        return w.ToArray();
    }

    public static byte[] OwnerReleased(ushort monsterId)
    {
        var w = new PacketWriter(OwnerReleasedSize);
        w.WriteUInt16(monsterId);
        return w.ToArray();
    }

    public static byte SpeedQuarter(float speed) => tPoseidon.Core.Game.Monsters.ClientSpeed.Quarter(speed);

    public static float DrawnSpeed(float speed) => tPoseidon.Core.Game.Monsters.ClientSpeed.Drawn(speed);

    public static byte[] Speed(ushort entityId, float speed)
    {
        var w = new PacketWriter(SpeedSize);
        w.WriteUInt16(entityId);
        w.WriteByte(SpeedQuarter(speed));
        return w.ToArray();
    }
}
