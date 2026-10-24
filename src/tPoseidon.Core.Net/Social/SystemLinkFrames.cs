namespace tPoseidon.Core.Net.Social;

public sealed record OfficerOrder(ushort AgencyId, ushort OrderId, ushort OrderType, ulong CharacterId, byte[] Data);

public sealed record PartyMirrorEntry(ulong CharacterId, ushort MemberId);

public sealed record PartyMirror(IReadOnlyList<PartyMirrorEntry> Others, ulong RecipientCharacterId, ulong MasterCharacterId, ushort PartyId);

public sealed record PartyQuestMember(ulong CharacterId, byte CompletionCount);

public sealed record PartyQuestBegin(
    ushort PartyId,
    ushort Zone,
    ulong CharacterId,
    ulong PartyUid,
    uint QuestId,
    ushort MonsterId,
    ushort TargetCount,
    uint StartMs,
    uint LimitMs,
    IReadOnlyList<PartyQuestMember> Members);

public sealed record PartyQuestTally(ulong RecipientCharacterId, uint QuestId, ushort TeamKills, IReadOnlyList<(ulong CharacterId, ushort Kills)> Members);

public sealed record PartyQuestRecovery(
    ulong RecipientCharacterId,
    ulong PartyUid,
    uint QuestId,
    ushort MonsterId,
    ushort TeamKills,
    ushort TargetCount,
    uint StartMs,
    uint LimitMs,
    byte RecipientCompletionCount,
    IReadOnlyList<(ulong CharacterId, ushort Kills)> Members);

public sealed record PartyQuestCancel(ulong CharacterId, uint QuestId, ushort Detail, uint Reason);

public static class SystemLinkFrames
{
    public const ushort SystemHello = 0x157D;
    public const ushort Order = 0x158A;
    public const ushort Report = 0x158B;
    public const ushort EnterZone = 0x158D;
    public const ushort LeaveZone = 0x158F;
    public const ushort LoggedOut = 0x1591;
    public const ushort CharacterTypeChanged = 0x159F;
    public const ushort LevelChanged = 0x15A0;
    public const ushort Died = 0x15A1;
    public const ushort Revived = 0x15A5;
    public const ushort BeginPartyQuest = 0x15B1;
    public const ushort PartyQuestBegun = 0x15B2;
    public const ushort PartyQuestNotBegun = 0x15B3;
    public const ushort PartyQuestKill = 0x15B5;
    public const ushort GuildPointForQuest = 0x15BB;
    public const ushort CancelPartyQuest = 0x15D5;

    public const ushort OrderCancel = 2;
    public const ushort OrderShareExp = 0x0C;
    public const ushort OrderTypeBonus = 0x0D;
    public const ushort OrderNotifyPartyPlayer = 0x10;
    public const ushort OrderCancelQuest = 0x12;
    public const ushort OrderUpdateQuest = 0x15;
    public const ushort OrderRecoverQuest = 0x16;
    public const ushort OrderGuildName = 0xE7;

    public const int MirrorSlots = 6;
    public const int MirrorLength = 78;
    public const int QuestTallyLength = 75;
    public const int QuestRecoveryLength = 96;
    public const uint QuestCancelByMaster = 3;

    public static byte[] Character(ulong characterId)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static byte[] CharacterTypeChangedFor(ulong characterId, byte type, byte job)
    {
        var w = new PacketWriter(10);
        w.WriteUInt64(characterId);
        w.WriteByte(type);
        w.WriteByte(job);
        return w.ToArray();
    }

    public static (ulong CharacterId, byte Type, byte Job) ReadCharacterTypeChanged(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadByte(), r.ReadByte());
    }

    public static byte[] CharacterAndZone(ulong characterId, ushort zone)
    {
        var w = new PacketWriter(10);
        w.WriteUInt64(characterId);
        w.WriteUInt16(zone);
        return w.ToArray();
    }

    public static (ulong CharacterId, ushort Zone) ReadCharacterAndZone(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadUInt16());
    }

    public static ulong ReadCharacter(ReadOnlySpan<byte> body) => new PacketReader(body).ReadUInt64();

    public static byte[] CharacterAndLevel(ulong characterId, uint level)
    {
        var w = new PacketWriter(12);
        w.WriteUInt64(characterId);
        w.WriteUInt32(level);
        return w.ToArray();
    }

    public static (ulong CharacterId, uint Level) ReadCharacterAndLevel(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadUInt32());
    }

    public static byte[] EncodeOrder(ushort zonePrefix, OfficerOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var w = new PacketWriter(2 + 18 + order.Data.Length);
        w.WriteUInt16(zonePrefix);
        w.WriteUInt16(order.AgencyId);
        w.WriteUInt16(order.OrderId);
        w.WriteUInt16(order.OrderType);
        w.WriteUInt64(order.CharacterId);
        w.WriteUInt32((uint)order.Data.Length);
        w.WriteBytes(order.Data);
        return w.ToArray();
    }

    public static (ushort ZonePrefix, OfficerOrder Order) DecodeOrder(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var zone = r.ReadUInt16();
        var agencyId = r.ReadUInt16();
        var orderId = r.ReadUInt16();
        var orderType = r.ReadUInt16();
        var characterId = r.ReadUInt64();
        var length = r.ReadUInt32();
        return (zone, new OfficerOrder(agencyId, orderId, orderType, characterId, r.ReadBytes(checked((int)length))));
    }

    public static byte[] CancelData(ushort orderId)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(orderId);
        return w.ToArray();
    }

    public static ushort ReadCancelledOrderId(ReadOnlySpan<byte> data) => new PacketReader(data).ReadUInt16();

    public static byte[] EncodeMirror(PartyMirror mirror)
    {
        ArgumentNullException.ThrowIfNull(mirror);
        if (mirror.Others.Count > MirrorSlots)
        {
            throw new ArgumentException($"A party mirror holds at most {MirrorSlots} other members.", nameof(mirror));
        }
        var w = new PacketWriter(MirrorLength);
        foreach (var other in mirror.Others)
        {
            w.WriteUInt64(other.CharacterId);
            w.WriteUInt16(other.MemberId);
        }
        w.WriteZero((MirrorSlots - mirror.Others.Count) * 10);
        w.WriteUInt64(mirror.RecipientCharacterId);
        w.WriteUInt64(mirror.MasterCharacterId);
        w.WriteUInt16(mirror.PartyId);
        return w.ToArray();
    }

    public static PartyMirror DecodeMirror(ReadOnlySpan<byte> data)
    {
        var r = new PacketReader(data);
        var others = new List<PartyMirrorEntry>();
        for (var i = 0; i < MirrorSlots; i++)
        {
            var characterId = r.ReadUInt64();
            var memberId = r.ReadUInt16();
            if (characterId != 0) others.Add(new PartyMirrorEntry(characterId, memberId));
        }
        return new PartyMirror(others, r.ReadUInt64(), r.ReadUInt64(), r.ReadUInt16());
    }

    public static byte[] EncodeBeginPartyQuest(PartyQuestBegin begin)
    {
        ArgumentNullException.ThrowIfNull(begin);
        var w = new PacketWriter(41 + begin.Members.Count * 9);
        w.WriteUInt16(begin.PartyId);
        w.WriteUInt16(begin.Zone);
        w.WriteUInt64(begin.CharacterId);
        w.WriteUInt64(begin.PartyUid);
        w.WriteUInt32(begin.QuestId);
        w.WriteUInt16(begin.MonsterId);
        w.WriteUInt16(begin.TargetCount);
        w.WriteUInt32(begin.StartMs);
        w.WriteUInt32(begin.LimitMs);
        w.WriteByte((byte)begin.Members.Count);
        foreach (var member in begin.Members)
        {
            w.WriteUInt64(member.CharacterId);
            w.WriteByte(member.CompletionCount);
        }
        return w.ToArray();
    }

    public static PartyQuestBegin DecodeBeginPartyQuest(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        var partyId = r.ReadUInt16();
        var zone = r.ReadUInt16();
        var characterId = r.ReadUInt64();
        var partyUid = r.ReadUInt64();
        var questId = r.ReadUInt32();
        var monsterId = r.ReadUInt16();
        var targetCount = r.ReadUInt16();
        var startMs = r.ReadUInt32();
        var limitMs = r.ReadUInt32();
        var count = r.ReadByte();
        var members = new List<PartyQuestMember>(count);
        for (var i = 0; i < count; i++) members.Add(new PartyQuestMember(r.ReadUInt64(), r.ReadByte()));
        return new PartyQuestBegin(partyId, zone, characterId, partyUid, questId, monsterId, targetCount, startMs, limitMs, members);
    }

    public static byte[] ZoneAndCharacter(ushort zone, ulong characterId)
    {
        var w = new PacketWriter(10);
        w.WriteUInt16(zone);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static byte[] KillReport(ulong characterId, ushort monsterId)
    {
        var w = new PacketWriter(10);
        w.WriteUInt64(characterId);
        w.WriteUInt16(monsterId);
        return w.ToArray();
    }

    public static (ulong CharacterId, ushort MonsterId) ReadKillReport(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadUInt16());
    }

    public static byte[] CharacterAndQuest(ulong characterId, uint questId)
    {
        var w = new PacketWriter(12);
        w.WriteUInt64(characterId);
        w.WriteUInt32(questId);
        return w.ToArray();
    }

    public static (ulong CharacterId, uint QuestId) ReadCharacterAndQuest(ReadOnlySpan<byte> body)
    {
        var r = new PacketReader(body);
        return (r.ReadUInt64(), r.ReadUInt32());
    }

    public static byte[] EncodeQuestTally(PartyQuestTally tally)
    {
        ArgumentNullException.ThrowIfNull(tally);
        if (tally.Members.Count > MirrorSlots) throw new ArgumentException($"A party quest tally holds at most {MirrorSlots} members.", nameof(tally));
        var w = new PacketWriter(QuestTallyLength);
        w.WriteUInt64(tally.RecipientCharacterId);
        w.WriteUInt32(tally.QuestId);
        w.WriteByte((byte)tally.Members.Count);
        w.WriteUInt16(tally.TeamKills);
        for (var i = 0; i < MirrorSlots; i++) w.WriteUInt64(i < tally.Members.Count ? tally.Members[i].CharacterId : 0);
        for (var i = 0; i < MirrorSlots; i++) w.WriteUInt16(i < tally.Members.Count ? tally.Members[i].Kills : (ushort)0);
        return w.ToArray();
    }

    public static PartyQuestTally DecodeQuestTally(ReadOnlySpan<byte> data)
    {
        var r = new PacketReader(data);
        var recipient = r.ReadUInt64();
        var questId = r.ReadUInt32();
        var count = r.ReadByte();
        var teamKills = r.ReadUInt16();
        var ids = new ulong[MirrorSlots];
        for (var i = 0; i < MirrorSlots; i++) ids[i] = r.ReadUInt64();
        var kills = new ushort[MirrorSlots];
        for (var i = 0; i < MirrorSlots; i++) kills[i] = r.ReadUInt16();
        var members = Enumerable.Range(0, Math.Min((int)count, MirrorSlots)).Select(i => (ids[i], kills[i])).ToArray();
        return new PartyQuestTally(recipient, questId, teamKills, members);
    }

    public static byte[] EncodeQuestRecovery(PartyQuestRecovery recovery)
    {
        ArgumentNullException.ThrowIfNull(recovery);
        if (recovery.Members.Count > MirrorSlots) throw new ArgumentException($"A party quest recovery holds at most {MirrorSlots} members.", nameof(recovery));
        var w = new PacketWriter(QuestRecoveryLength);
        w.WriteUInt64(recovery.RecipientCharacterId);
        w.WriteUInt64(recovery.PartyUid);
        w.WriteUInt32(recovery.QuestId);
        w.WriteByte((byte)recovery.Members.Count);
        w.WriteUInt16(recovery.MonsterId);
        w.WriteUInt16(recovery.TeamKills);
        w.WriteUInt16(recovery.TargetCount);
        w.WriteUInt32(recovery.StartMs);
        w.WriteUInt32(recovery.LimitMs);
        w.WriteByte(recovery.RecipientCompletionCount);
        for (var i = 0; i < MirrorSlots; i++) w.WriteUInt64(i < recovery.Members.Count ? recovery.Members[i].CharacterId : 0);
        for (var i = 0; i < MirrorSlots; i++) w.WriteUInt16(i < recovery.Members.Count ? recovery.Members[i].Kills : (ushort)0);
        return w.ToArray();
    }

    public static PartyQuestRecovery DecodeQuestRecovery(ReadOnlySpan<byte> data)
    {
        var r = new PacketReader(data);
        var recipient = r.ReadUInt64();
        var partyUid = r.ReadUInt64();
        var questId = r.ReadUInt32();
        var count = r.ReadByte();
        var monsterId = r.ReadUInt16();
        var teamKills = r.ReadUInt16();
        var targetCount = r.ReadUInt16();
        var startMs = r.ReadUInt32();
        var limitMs = r.ReadUInt32();
        var completions = r.ReadByte();
        var ids = new ulong[MirrorSlots];
        for (var i = 0; i < MirrorSlots; i++) ids[i] = r.ReadUInt64();
        var kills = new ushort[MirrorSlots];
        for (var i = 0; i < MirrorSlots; i++) kills[i] = r.ReadUInt16();
        var members = Enumerable.Range(0, Math.Min((int)count, MirrorSlots)).Select(i => (ids[i], kills[i])).ToArray();
        return new PartyQuestRecovery(recipient, partyUid, questId, monsterId, teamKills, targetCount, startMs, limitMs, completions, members);
    }

    public static byte[] EncodeQuestCancel(PartyQuestCancel cancel)
    {
        ArgumentNullException.ThrowIfNull(cancel);
        var w = new PacketWriter(18);
        w.WriteUInt64(cancel.CharacterId);
        w.WriteUInt32(cancel.QuestId);
        w.WriteUInt16(cancel.Detail);
        w.WriteUInt32(cancel.Reason);
        return w.ToArray();
    }

    public static PartyQuestCancel DecodeQuestCancel(ReadOnlySpan<byte> data)
    {
        var r = new PacketReader(data);
        return new PartyQuestCancel(r.ReadUInt64(), r.ReadUInt32(), r.ReadUInt16(), r.ReadUInt32());
    }
}
