using tPoseidon.Core.Common;
using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public enum CharStoreSubOp : byte
{
    GetCharactersForAccount = 0x01,
    GetCharacterById = 0x02,
    AddCharacter = 0x03,
    DeleteCharacter = 0x04,
    SetCharacterMap = 0x05,
    SetCharacterPosition = 0x06,
    SaveCharacterStats = 0x07,
    GetSnapshot = 0x08,
    SetCharacterHome = 0x09,
    EditCharacter = 0x0A,
}

public static class CharStoreWire
{
    public static byte[] EncodeCharacter(Character c)
    {
        ArgumentNullException.ThrowIfNull(c);
        var writer = new PacketWriter(256);
        WriteCharacter(writer, c);
        return writer.ToArray();
    }

    public static Character DecodeCharacter(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        return ReadCharacter(ref reader);
    }

    public static byte[] EncodeCharacterList(IReadOnlyList<Character> list)
    {
        ArgumentNullException.ThrowIfNull(list);
        if (list.Count > ushort.MaxValue)
        {
            throw new InvalidPacketException($"character list has {list.Count} entries; exceeds u16 count max.");
        }
        var writer = new PacketWriter(2 + (list.Count * 256));
        writer.WriteUInt16((ushort)list.Count);
        foreach (var c in list)
        {
            WriteCharacter(writer, c);
        }
        return writer.ToArray();
    }

    public static IReadOnlyList<Character> DecodeCharacterList(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var count = reader.ReadUInt16();
        var list = new List<Character>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(ReadCharacter(ref reader));
        }
        return list;
    }

    public static byte[] EncodeStatsSnapshot(CharacterStatsSnapshot s)
    {
        ArgumentNullException.ThrowIfNull(s);
        var writer = new PacketWriter(64);
        WriteStatsSnapshot(writer, s);
        return writer.ToArray();
    }

    public static CharacterStatsSnapshot DecodeStatsSnapshot(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        return ReadStatsSnapshot(ref reader);
    }

    public static byte[] EncodeEdit(CharacterEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        var writer = new PacketWriter(48);
        writer.WriteByte((byte)edit.Kind);
        WriteString(writer, edit.NewName);
        writer.WriteInt32(edit.ItemIndex);
        writer.WriteByte(edit.HairSlot);
        writer.WriteUInt16(edit.HairColor);
        writer.WriteByte((byte)edit.NewSpecies);
        writer.WriteUInt16(edit.EarItemIndex);
        writer.WriteUInt16(edit.TailItemIndex);
        writer.WriteByte(edit.PtsP);
        writer.WriteByte(edit.PtsM);
        writer.WriteByte(edit.PtsS);
        writer.WriteByte(edit.PtsC);
        writer.WriteUInt16(edit.WarehouseSlots);
        return writer.ToArray();
    }

    public static CharacterEdit DecodeEdit(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        return new CharacterEdit
        {
            Kind = (CharacterEditKind)reader.ReadByte(),
            NewName = ReadString(ref reader),
            ItemIndex = reader.ReadInt32(),
            HairSlot = reader.ReadByte(),
            HairColor = reader.ReadUInt16(),
            NewSpecies = (Species)reader.ReadByte(),
            EarItemIndex = reader.ReadUInt16(),
            TailItemIndex = reader.ReadUInt16(),
            PtsP = reader.ReadByte(),
            PtsM = reader.ReadByte(),
            PtsS = reader.ReadByte(),
            PtsC = reader.ReadByte(),
            WarehouseSlots = reader.ReadUInt16(),
        };
    }

    private static void WriteCharacter(PacketWriter writer, Character c)
    {
        writer.WriteUInt64(c.CharacterId);
        writer.WriteUInt32(c.OwnerAccountId);
        writer.WriteByte(c.Slot);
        WriteString(writer, c.Name);
        WriteString(writer, c.AccountLabel);
        writer.WriteInt16((short)c.Species);
        writer.WriteInt16((short)c.FType);
        writer.WriteByte(c.PtsP);
        writer.WriteByte(c.PtsM);
        writer.WriteByte(c.PtsS);
        writer.WriteByte(c.PtsC);
        writer.WriteByte(c.JobTier);
        writer.WriteUInt16(c.Hair);
        writer.WriteInt64(c.Money);
        writer.WriteUInt16(c.Level);
        writer.WriteUInt16(c.StarterEarItemIndex);
        writer.WriteUInt16(c.StarterTailItemIndex);
        writer.WriteUInt16(c.PositionX);
        writer.WriteUInt16(c.PositionY);
        writer.WriteUInt16(c.MapId);
        writer.WriteInt64(c.CurrentHp);
        writer.WriteInt64(c.CurrentMp);
        writer.WriteUInt64(c.Exp);
        writer.WriteUInt64(c.TmExpPower);
        writer.WriteUInt64(c.TmExpMagic);
        writer.WriteUInt64(c.TmExpSense);
        writer.WriteUInt64(c.TmExpCharm);
        writer.WriteUInt16(c.WarehouseSlots);
        writer.WriteInt64(c.LoginTime.UtcTicks);
        writer.WriteInt64(c.LogoutTime.UtcTicks);
        writer.WriteUInt32(c.TotalConnectedSeconds);
        writer.WriteUInt16(c.ApPoints);
        writer.WriteUInt16(c.AcPoints);
        writer.WriteUInt16(c.DxPoints);
        writer.WriteUInt16(c.MaPoints);
        writer.WriteUInt16(c.MdPoints);
        writer.WriteUInt16(c.DaPoints);
        writer.WriteUInt16(c.LkPoints);
        writer.WriteUInt16(c.DpPoints);
        writer.WriteUInt16(c.HvPoints);
        writer.WriteUInt16(c.MaxHpPoints);
        writer.WriteUInt16(c.MaxMpPoints);
        writer.WriteUInt16(c.MaxWtPoints);
        writer.WriteUInt16(c.AttrPoints);
        writer.WriteUInt16(c.TmLevelPower);
        writer.WriteUInt16(c.TmLevelMagic);
        writer.WriteUInt16(c.TmLevelSense);
        writer.WriteUInt16(c.TmLevelCharm);
        writer.WriteUInt16(c.TmPoint);
        writer.WriteUInt16(c.TmLevel);
        writer.WriteInt64(c.TmExp);
        writer.WriteUInt16(c.Type2);
        writer.WriteByte(c.Job2);
        writer.WriteInt64(c.Job2ChangedAt.UtcTicks);
        writer.WriteUInt16(c.Type3);
        writer.WriteByte(c.Job3);
        writer.WriteInt64(c.Job3ChangedAt.UtcTicks);
        writer.WriteByte(c.ChangeJobQuestState);
        writer.WriteUInt64(c.DepthCount);
        writer.WriteUInt32(c.DigCount);
        writer.WriteUInt16(c.Hair2);
        writer.WriteUInt16(c.Hair3);
        writer.WriteByte(c.JobState);
        writer.WriteByte(c.CharState);
        writer.WriteUInt16(c.Home);
        writer.WriteByte(c.CharAuthority);
        writer.WriteInt16(c.GuildState);
        writer.WriteByte((byte)(c.IsGuildChief ? 1 : 0));
        writer.WriteInt64(c.GenTime.UtcTicks);
    }

    private static Character ReadCharacter(ref PacketReader reader) => new()
    {
        CharacterId = reader.ReadUInt64(),
        OwnerAccountId = reader.ReadUInt32(),
        Slot = reader.ReadByte(),
        Name = ReadString(ref reader),
        AccountLabel = ReadString(ref reader),
        Species = (Species)reader.ReadInt16(),
        FType = (FType)reader.ReadInt16(),
        PtsP = reader.ReadByte(),
        PtsM = reader.ReadByte(),
        PtsS = reader.ReadByte(),
        PtsC = reader.ReadByte(),
        JobTier = reader.ReadByte(),
        Hair = reader.ReadUInt16(),
        Money = reader.ReadInt64(),
        Level = reader.ReadUInt16(),
        StarterEarItemIndex = reader.ReadUInt16(),
        StarterTailItemIndex = reader.ReadUInt16(),
        PositionX = reader.ReadUInt16(),
        PositionY = reader.ReadUInt16(),
        MapId = reader.ReadUInt16(),
        CurrentHp = reader.ReadInt64(),
        CurrentMp = reader.ReadInt64(),
        Exp = reader.ReadUInt64(),
        TmExpPower = reader.ReadUInt64(),
        TmExpMagic = reader.ReadUInt64(),
        TmExpSense = reader.ReadUInt64(),
        TmExpCharm = reader.ReadUInt64(),
        WarehouseSlots = reader.ReadUInt16(),
        LoginTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        LogoutTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        TotalConnectedSeconds = reader.ReadUInt32(),
        ApPoints = reader.ReadUInt16(),
        AcPoints = reader.ReadUInt16(),
        DxPoints = reader.ReadUInt16(),
        MaPoints = reader.ReadUInt16(),
        MdPoints = reader.ReadUInt16(),
        DaPoints = reader.ReadUInt16(),
        LkPoints = reader.ReadUInt16(),
        DpPoints = reader.ReadUInt16(),
        HvPoints = reader.ReadUInt16(),
        MaxHpPoints = reader.ReadUInt16(),
        MaxMpPoints = reader.ReadUInt16(),
        MaxWtPoints = reader.ReadUInt16(),
        AttrPoints = reader.ReadUInt16(),
        TmLevelPower = reader.ReadUInt16(),
        TmLevelMagic = reader.ReadUInt16(),
        TmLevelSense = reader.ReadUInt16(),
        TmLevelCharm = reader.ReadUInt16(),
        TmPoint = reader.ReadUInt16(),
        TmLevel = reader.ReadUInt16(),
        TmExp = reader.ReadInt64(),
        Type2 = reader.ReadUInt16(),
        Job2 = reader.ReadByte(),
        Job2ChangedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        Type3 = reader.ReadUInt16(),
        Job3 = reader.ReadByte(),
        Job3ChangedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        ChangeJobQuestState = reader.ReadByte(),
        DepthCount = reader.ReadUInt64(),
        DigCount = reader.ReadUInt32(),
        Hair2 = reader.ReadUInt16(),
        Hair3 = reader.ReadUInt16(),
        JobState = reader.ReadByte(),
        CharState = reader.ReadByte(),
        Home = reader.ReadUInt16(),
        CharAuthority = reader.ReadByte(),
        GuildState = reader.ReadInt16(),
        IsGuildChief = reader.ReadByte() != 0,
        GenTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
    };

    private static void WriteStatsSnapshot(PacketWriter writer, CharacterStatsSnapshot s)
    {
        writer.WriteInt64(s.CurrentHp);
        writer.WriteInt64(s.CurrentMp);
        writer.WriteUInt16(s.Level);
        writer.WriteUInt64(s.Exp);
        writer.WriteInt64(s.Money);
        writer.WriteInt64(s.TmExp);
        writer.WriteUInt16(s.TmLevel);
        writer.WriteUInt16(s.TmPoint);
        writer.WriteUInt16(s.ApPoints);
        writer.WriteUInt16(s.AcPoints);
        writer.WriteUInt16(s.DxPoints);
        writer.WriteUInt16(s.MaPoints);
        writer.WriteUInt16(s.MdPoints);
        writer.WriteUInt16(s.DaPoints);
        writer.WriteUInt16(s.LkPoints);
        writer.WriteUInt16(s.DpPoints);
        writer.WriteUInt16(s.HvPoints);
        writer.WriteUInt16(s.MaxHpPoints);
        writer.WriteUInt16(s.MaxMpPoints);
        writer.WriteUInt16(s.MaxWtPoints);
        writer.WriteUInt16(s.AttrPoints);
        writer.WriteUInt16(s.TmLevelPower);
        writer.WriteUInt16(s.TmLevelMagic);
        writer.WriteUInt16(s.TmLevelSense);
        writer.WriteUInt16(s.TmLevelCharm);
    }

    private static CharacterStatsSnapshot ReadStatsSnapshot(ref PacketReader reader) => new()
    {
        CurrentHp = reader.ReadInt64(),
        CurrentMp = reader.ReadInt64(),
        Level = reader.ReadUInt16(),
        Exp = reader.ReadUInt64(),
        Money = reader.ReadInt64(),
        TmExp = reader.ReadInt64(),
        TmLevel = reader.ReadUInt16(),
        TmPoint = reader.ReadUInt16(),
        ApPoints = reader.ReadUInt16(),
        AcPoints = reader.ReadUInt16(),
        DxPoints = reader.ReadUInt16(),
        MaPoints = reader.ReadUInt16(),
        MdPoints = reader.ReadUInt16(),
        DaPoints = reader.ReadUInt16(),
        LkPoints = reader.ReadUInt16(),
        DpPoints = reader.ReadUInt16(),
        HvPoints = reader.ReadUInt16(),
        MaxHpPoints = reader.ReadUInt16(),
        MaxMpPoints = reader.ReadUInt16(),
        MaxWtPoints = reader.ReadUInt16(),
        AttrPoints = reader.ReadUInt16(),
        TmLevelPower = reader.ReadUInt16(),
        TmLevelMagic = reader.ReadUInt16(),
        TmLevelSense = reader.ReadUInt16(),
        TmLevelCharm = reader.ReadUInt16(),
    };

    private static void WriteString(PacketWriter writer, string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidPacketException($"string is {bytes.Length} bytes; exceeds u16 length prefix max.");
        }
        writer.WriteUInt16((ushort)bytes.Length);
        writer.WriteBytes(bytes);
    }

    private static string ReadString(ref PacketReader reader)
    {
        var len = reader.ReadUInt16();
        return System.Text.Encoding.UTF8.GetString(reader.ReadBytes(len));
    }
}
