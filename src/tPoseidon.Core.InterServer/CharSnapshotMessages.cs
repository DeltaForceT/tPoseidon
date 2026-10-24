using tPoseidon.Core.Data;
using tPoseidon.Core.Net;

namespace tPoseidon.Core.InterServer;

public static class CharSnapshotWire
{
    public static byte[] Encode(CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var writer = new PacketWriter(2048);

        var charBody = CharStoreWire.EncodeCharacter(snapshot.Character);
        writer.WriteInt32(charBody.Length);
        writer.WriteBytes(charBody);

        WriteItemList(writer, snapshot.EquippedItems);
        WriteItemList(writer, snapshot.InventoryItems);
        WriteSkillList(writer, snapshot.Skills);
        WriteQuestList(writer, snapshot.ActiveQuests);

        WriteUniqueDuty(writer, snapshot.UniqueDutyInfo);
        WriteAwakening(writer, snapshot.Awakening);
        writer.WriteUInt64(snapshot.CoupleCode);
        WritePkInfo(writer, snapshot.PkInfo);
        WriteBlock(writer, snapshot.ActiveBlock);

        return writer.ToArray();
    }

    public static CharacterSnapshot Decode(ReadOnlySpan<byte> body)
    {
        var reader = new PacketReader(body);
        var charLen = reader.ReadInt32();
        var charBytes = reader.ReadBytes(charLen);
        var character = CharStoreWire.DecodeCharacter(charBytes);

        var equipped = ReadItemList(ref reader);
        var inventory = ReadItemList(ref reader);
        var skills = ReadSkillList(ref reader);
        var quests = ReadQuestList(ref reader);

        var uniqueDuty = ReadUniqueDuty(ref reader);
        var awakening = ReadAwakening(ref reader);
        var coupleCode = reader.ReadUInt64();
        var pkInfo = ReadPkInfo(ref reader);
        var activeBlock = ReadBlock(ref reader);

        return new CharacterSnapshot
        {
            Character = character,
            EquippedItems = equipped,
            InventoryItems = inventory,
            Skills = skills,
            ActiveQuests = quests,
            UniqueDutyInfo = uniqueDuty,
            Awakening = awakening,
            CoupleCode = coupleCode,
            PkInfo = pkInfo,
            ActiveBlock = activeBlock,
        };
    }

    private static void WriteItemList(PacketWriter writer, IReadOnlyList<Item> items)
    {
        writer.WriteUInt16((ushort)items.Count);
        foreach (var item in items) WriteItem(writer, item);
    }

    private static List<Item> ReadItemList(ref PacketReader reader)
    {
        var count = reader.ReadUInt16();
        var list = new List<Item>(count);
        for (var i = 0; i < count; i++) list.Add(ReadItem(ref reader));
        return list;
    }

    private static void WriteItem(PacketWriter writer, Item item)
    {
        writer.WriteUInt64(item.ItemUid);
        writer.WriteInt32(item.ItemIndex);
        writer.WriteByte(item.ItemClass);
        writer.WriteUInt16(item.Stack);
        writer.WriteInt16(item.ItemAttr);
        writer.WriteByte(item.ItemCharged);
        writer.WriteInt16(item.RefineType);
        writer.WriteByte(item.RefineLevel);
        writer.WriteByte(item.RefineState);
        writer.WriteByte(item.ItemSlot);
        writer.WriteInt64(item.BreakedAt.UtcTicks);
        writer.WriteUInt64(item.CharacterId);
        writer.WriteByte(item.SlotKind);
        writer.WriteByte(item.SlotPosition);
    }

    private static Item ReadItem(ref PacketReader reader)
    {
        return new Item
        {
            ItemUid = reader.ReadUInt64(),
            ItemIndex = reader.ReadInt32(),
            ItemClass = reader.ReadByte(),
            Stack = reader.ReadUInt16(),
            ItemAttr = reader.ReadInt16(),
            ItemCharged = reader.ReadByte(),
            RefineType = reader.ReadInt16(),
            RefineLevel = reader.ReadByte(),
            RefineState = reader.ReadByte(),
            ItemSlot = reader.ReadByte(),
            BreakedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
            CharacterId = reader.ReadUInt64(),
            SlotKind = reader.ReadByte(),
            SlotPosition = reader.ReadByte(),
        };
    }

    private static void WriteSkillList(PacketWriter writer, IReadOnlyList<Skill> skills)
    {
        writer.WriteUInt16((ushort)skills.Count);
        foreach (var s in skills)
        {
            writer.WriteUInt64(s.CharacterId);
            writer.WriteInt32(s.SkillId);
            writer.WriteInt32(s.SkillLevel);
            writer.WriteInt64(s.UpdatedAt.UtcTicks);
        }
    }

    private static List<Skill> ReadSkillList(ref PacketReader reader)
    {
        var count = reader.ReadUInt16();
        var list = new List<Skill>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(new Skill
            {
                CharacterId = reader.ReadUInt64(),
                SkillId = reader.ReadInt32(),
                SkillLevel = reader.ReadInt32(),
                UpdatedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
            });
        }
        return list;
    }

    private static void WriteQuestList(PacketWriter writer, IReadOnlyList<Quest> quests)
    {
        writer.WriteUInt16((ushort)quests.Count);
        foreach (var q in quests)
        {
            writer.WriteUInt64(q.CharacterId);
            writer.WriteByte(q.QuestType);
            writer.WriteInt32(q.QuestId);
            writer.WriteByte(q.CompletionCount);
            writer.WriteInt32(q.MonsterId);
            writer.WriteInt16(q.KillCount);
            writer.WriteInt32(q.RecordTime);
            writer.WriteInt64(q.AcceptedAt.UtcTicks);
        }
    }

    private static List<Quest> ReadQuestList(ref PacketReader reader)
    {
        var count = reader.ReadUInt16();
        var list = new List<Quest>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(new Quest
            {
                CharacterId = reader.ReadUInt64(),
                QuestType = reader.ReadByte(),
                QuestId = reader.ReadInt32(),
                CompletionCount = reader.ReadByte(),
                MonsterId = reader.ReadInt32(),
                KillCount = reader.ReadInt16(),
                RecordTime = reader.ReadInt32(),
                AcceptedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
            });
        }
        return list;
    }

    private static void WriteUniqueDuty(PacketWriter writer, UniqueCharDutyInfo? info)
    {
        writer.WriteByte(info is null ? (byte)0 : (byte)1);
        if (info is null) return;
        writer.WriteUInt64(info.CharacterId);
        writer.WriteInt16(info.BossMonMedalCount);
        writer.WriteInt16(info.UniqueItemCount);
        writer.WriteByte((byte)(info.DutyEndAt.HasValue ? 1 : 0));
        writer.WriteInt64(info.DutyEndAt?.UtcTicks ?? 0);
    }

    private static UniqueCharDutyInfo? ReadUniqueDuty(ref PacketReader reader)
    {
        if (reader.ReadByte() == 0) return null;
        var charId = reader.ReadUInt64();
        var medal = reader.ReadInt16();
        var itemCount = reader.ReadInt16();
        var hasEnd = reader.ReadByte() != 0;
        var endTicks = reader.ReadInt64();
        return new UniqueCharDutyInfo
        {
            CharacterId = charId,
            BossMonMedalCount = medal,
            UniqueItemCount = itemCount,
            DutyEndAt = hasEnd ? new DateTimeOffset(endTicks, TimeSpan.Zero) : null,
        };
    }

    private static void WriteAwakening(PacketWriter writer, CharAwakening? awakening)
    {
        writer.WriteByte(awakening is null ? (byte)0 : (byte)1);
        if (awakening is null) return;
        writer.WriteUInt64(awakening.CharacterId);
        writer.WriteByte(awakening.SoulSeedFlag);
        writer.WriteByte(awakening.AwakeningFlag);
        writer.WriteInt16(awakening.SlotMaxCount);
        writer.WriteInt64(awakening.CreatedAt.UtcTicks);
    }

    private static CharAwakening? ReadAwakening(ref PacketReader reader)
    {
        if (reader.ReadByte() == 0) return null;
        return new CharAwakening
        {
            CharacterId = reader.ReadUInt64(),
            SoulSeedFlag = reader.ReadByte(),
            AwakeningFlag = reader.ReadByte(),
            SlotMaxCount = reader.ReadInt16(),
            CreatedAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        };
    }

    private static void WritePkInfo(PacketWriter writer, PkInfo? pk)
    {
        writer.WriteByte(pk is null ? (byte)0 : (byte)1);
        if (pk is null) return;
        writer.WriteUInt64(pk.CharacterId);
        writer.WriteInt32(pk.KillTodayCount);
        writer.WriteInt32(pk.KillTotalCount);
        writer.WriteInt32(pk.DeathTodayCount);
        writer.WriteInt32(pk.DeathTotalCount);
        writer.WriteInt64(pk.LatestUpdateAt.UtcTicks);
    }

    private static PkInfo? ReadPkInfo(ref PacketReader reader)
    {
        if (reader.ReadByte() == 0) return null;
        return new PkInfo
        {
            CharacterId = reader.ReadUInt64(),
            KillTodayCount = reader.ReadInt32(),
            KillTotalCount = reader.ReadInt32(),
            DeathTodayCount = reader.ReadInt32(),
            DeathTotalCount = reader.ReadInt32(),
            LatestUpdateAt = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
        };
    }

    private static void WriteBlock(PacketWriter writer, AccountBlock? block)
    {
        writer.WriteByte(block is null ? (byte)0 : (byte)1);
        if (block is null) return;
        writer.WriteInt64(block.BlockSeq);
        writer.WriteUInt32(block.TargetAccountId);
        writer.WriteByte(block.BlockType);
        writer.WriteByte(block.BlockReason);
        writer.WriteByte((byte)(block.GmAccountId.HasValue ? 1 : 0));
        writer.WriteUInt32(block.GmAccountId ?? 0);
        writer.WriteInt64(block.BlockFrom.UtcTicks);
        writer.WriteInt64(block.BlockTo.UtcTicks);
        WriteShortString(writer, block.Description);
        writer.WriteInt64(block.CreatedAt.UtcTicks);
    }

    private static AccountBlock? ReadBlock(ref PacketReader reader)
    {
        if (reader.ReadByte() == 0) return null;
        var seq = reader.ReadInt64();
        var target = reader.ReadUInt32();
        var type = reader.ReadByte();
        var reason = reader.ReadByte();
        var hasGm = reader.ReadByte() != 0;
        var gmId = reader.ReadUInt32();
        var from = reader.ReadInt64();
        var to = reader.ReadInt64();
        var desc = ReadShortString(ref reader);
        var created = reader.ReadInt64();
        return new AccountBlock
        {
            BlockSeq = seq,
            TargetAccountId = target,
            BlockType = type,
            BlockReason = reason,
            GmAccountId = hasGm ? gmId : null,
            BlockFrom = new DateTimeOffset(from, TimeSpan.Zero),
            BlockTo = new DateTimeOffset(to, TimeSpan.Zero),
            Description = desc,
            CreatedAt = new DateTimeOffset(created, TimeSpan.Zero),
        };
    }

    private static void WriteShortString(PacketWriter writer, string s)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(s ?? string.Empty);
        if (bytes.Length > ushort.MaxValue)
        {
            throw new InvalidOperationException($"string too long: {bytes.Length} bytes");
        }
        writer.WriteUInt16((ushort)bytes.Length);
        writer.WriteBytes(bytes);
    }

    private static string ReadShortString(ref PacketReader reader)
    {
        var len = reader.ReadUInt16();
        if (len == 0) return string.Empty;
        var bytes = new byte[len];
        for (var i = 0; i < len; i++) bytes[i] = reader.ReadByte();
        return System.Text.Encoding.UTF8.GetString(bytes);
    }
}
