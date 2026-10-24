namespace tPoseidon.Core.Net.World;

public sealed record CharacterStatBlock(
    IReadOnlyList<short> StatPoints,
    ushort BonusPoints,
    ushort TmPoint,
    ushort TmLevel,
    ushort Level,
    byte LoginMark)
{
    public const int Size = 33;
    public const int StatCount = 12;
    public const byte LoginMarkAsStored = 0;
    public const byte LoginMarkWhenUnset = 1;

    public void WriteTo(PacketWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (StatPoints.Count != StatCount)
        {
            throw new InvalidOperationException($"A stat block carries {StatCount} stats, got {StatPoints.Count}.");
        }
        foreach (var points in StatPoints) writer.WriteInt16(points);
        writer.WriteUInt16(BonusPoints);
        writer.WriteUInt16(TmPoint);
        writer.WriteUInt16(TmLevel);
        writer.WriteUInt16(Level);
        writer.WriteByte(LoginMark);
    }

    public static CharacterStatBlock Read(ref PacketReader reader)
    {
        var points = new short[StatCount];
        for (var stat = 0; stat < StatCount; stat++) points[stat] = reader.ReadInt16();
        return new CharacterStatBlock(points, reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadUInt16(), reader.ReadByte());
    }
}
