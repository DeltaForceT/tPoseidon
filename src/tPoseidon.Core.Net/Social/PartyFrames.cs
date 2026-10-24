using System.Text;

namespace tPoseidon.Core.Net.Social;

public sealed record PartyMemberInfo(string Name, ushort Kind, ushort Zone, ushort MemberId, ushort Level, ulong CharacterId);

public sealed record PartyDivisionShare(ushort MemberId, byte Percent);

public static class PartyFrames
{
    public const ushort Make = 0x1B59;
    public const ushort ChangeDivision = 0x1B5C;
    public const ushort Rename = 0x1B5F;
    public const ushort ChangeMaster = 0x1B62;
    public const ushort Invite = 0x1B65;
    public const ushort AcceptInvite = 0x1B69;
    public const ushort RefuseInvite = 0x1B6C;
    public const ushort Leave = 0x1B6D;
    public const ushort Kick = 0x1B71;
    public const ushort Chat = 0x1B82;
    public const ushort KickOffline = 0x1B87;
    public const ushort SetItemShare = 0x1B8E;
    public const ushort MemberFlagOn = 0x1B92;
    public const ushort MemberFlagOff = 0x1B93;
    public const ushort AskToJoin = 0x1B9A;
    public const ushort AcceptJoin = 0x1B9D;
    public const ushort RefuseJoin = 0x1BA0;
    public const ushort Dissolve = 0x1BA3;
    public const ushort AskToJoinThroughMember = 0x1BA5;
    public const ushort SetSecondOption = 0x1BA7;
    public const ushort InfoRequest = 0x1BA8;
    public const ushort CancelInvite = 0x1BAB;
    public const ushort RemovePartyQuest = 0x1BB0;

    public const ushort Formed = 0x1B5A;
    public const ushort MakeFailed = 0x1B5B;
    public const ushort DivisionChanged = 0x1B5D;
    public const ushort DivisionRefused = 0x1B5E;
    public const ushort RenameDone = 0x1B60;
    public const ushort RenameRefused = 0x1B61;
    public const ushort MasterChangeDone = 0x1B63;
    public const ushort MasterChangeRefused = 0x1B64;
    public const ushort JoinedNotice = 0x1B66;
    public const ushort InviteFailed = 0x1B67;
    public const ushort Invitation = 0x1B68;
    public const ushort JoinState = 0x1B6A;
    public const ushort AcceptFailed = 0x1B6B;
    public const ushort LeaveDone = 0x1B6D;
    public const ushort KickDone = 0x1B72;
    public const ushort KickRefused = 0x1B73;
    public const ushort MemberJoined = 0x1B79;
    public const ushort MemberLeft = 0x1B7A;
    public const ushort MemberZoneCleared = 0x1B7B;
    public const ushort MemberZone = 0x1B7C;
    public const ushort MemberKicked = 0x1B7D;
    public const ushort Renamed = 0x1B7E;
    public const ushort Division = 0x1B7F;
    public const ushort ChatLine = 0x1B83;
    public const ushort MasterChanged = 0x1B84;
    public const ushort MemberReturned = 0x1B85;
    public const ushort MemberOffline = 0x1B86;
    public const ushort OfflineMemberRemoved = 0x1B88;
    public const ushort OfflineMemberNotFound = 0x1B89;
    public const ushort OfflineMemberExpired = 0x1B8A;
    public const ushort Made = 0x1B8B;
    public const ushort Broken = 0x1B8C;
    public const ushort ItemShareDenied = 0x1B8F;
    public const ushort ItemShareChanged = 0x1B90;
    public const ushort MemberFlagRaised = 0x1B96;
    public const ushort MemberFlagCleared = 0x1B97;
    public const ushort JoinAskRelayed = 0x1B9A;
    public const ushort AskResult = 0x1B9C;
    public const ushort MasterResult = 0x1B9F;
    public const ushort JoinDenied = 0x1BA0;
    public const ushort JoinRefuseRefused = 0x1BA2;
    public const ushort CandidateHasParty = 0x1BA4;
    public const ushort SecondOptionChanged = 0x1BA6;
    public const ushort Info = 0x1BA9;
    public const ushort InfoFailed = 0x1BAA;
    public const ushort InviteCancelled = 0x1BAB;
    public const ushort LevelChanged = 0x1BAC;
    public const ushort MemberStateChanged = 0x1BAD;
    public const ushort MemberRenamed = 0x1BAF;
    public const ushort PartyBonus = 0x1BAE;

    public const int MemberInfoLength = 60;
    public const int MemberNameLength = 20;

    public static byte[] Empty() => Array.Empty<byte>();

    public static byte[] Code(byte code) => new[] { code };

    public static byte[] Code32(uint code)
    {
        var w = new PacketWriter(4);
        w.WriteUInt32(code);
        return w.ToArray();
    }

    public static byte[] Member(ushort memberId)
    {
        var w = new PacketWriter(2);
        w.WriteUInt16(memberId);
        return w.ToArray();
    }

    public static byte[] Character(ulong characterId)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static byte[] Text(string text, Encoding encoding)
    {
        var w = new PacketWriter(32);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static byte[] NameAndCode(string name, byte code, Encoding encoding)
    {
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, encoding);
        w.WriteByte(code);
        return w.ToArray();
    }

    public static byte[] MemberPair(ushort memberId, ushort otherMemberId)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(memberId);
        w.WriteUInt16(otherMemberId);
        return w.ToArray();
    }

    public static byte[] PartyMade(ushort partyId, ushort makerMemberId) => MemberPair(partyId, makerMemberId);

    public static byte[] PartyFormed(ushort partyId, string partyName, ushort inviterMemberId, Encoding encoding)
    {
        var w = new PacketWriter(40);
        w.WriteUInt16(partyId);
        w.WriteNulTerminated(partyName, encoding);
        w.WriteUInt16(inviterMemberId);
        return w.ToArray();
    }

    public static byte[] InvitationFor(string partyName, string inviterName, byte memberCount, byte divisionOrFlag, byte itemShare, ushort averageLevel, Encoding encoding)
    {
        var w = new PacketWriter(64);
        w.WriteNulTerminated(partyName, encoding);
        w.WriteNulTerminated(inviterName, encoding);
        w.WriteByte(memberCount);
        w.WriteByte(divisionOrFlag);
        w.WriteByte(itemShare);
        w.WriteUInt16(averageLevel);
        return w.ToArray();
    }

    public static byte[] JoinStateFor(
        ushort partyId,
        string partyName,
        ushort masterMemberId,
        ushort joinerMemberId,
        byte divisionType,
        bool levelFlag,
        byte itemShare,
        byte secondOption,
        IReadOnlyList<PartyMemberInfo> online,
        IReadOnlyList<PartyMemberInfo> offline,
        Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(online);
        ArgumentNullException.ThrowIfNull(offline);
        var w = new PacketWriter(64 + (online.Count + offline.Count) * MemberInfoLength);
        w.WriteUInt16(partyId);
        w.WriteNulTerminated(partyName, encoding);
        w.WriteUInt16(masterMemberId);
        w.WriteUInt16(joinerMemberId);
        w.WriteByte(divisionType);
        w.WriteByte((byte)online.Count);
        w.WriteUInt16(levelFlag ? (ushort)1 : (ushort)0);
        w.WriteByte(itemShare);
        w.WriteByte(secondOption);
        foreach (var member in online) WriteMemberInfo(w, member, encoding);
        w.WriteByte((byte)offline.Count);
        foreach (var member in offline) WriteMemberInfo(w, member, encoding);
        return w.ToArray();
    }

    public static byte[] MemberJoinedFor(PartyMemberInfo member, bool levelFlag, Encoding encoding)
    {
        var w = new PacketWriter(MemberInfoLength + 2);
        WriteMemberInfo(w, member, encoding);
        w.WriteUInt16(levelFlag ? (ushort)1 : (ushort)0);
        return w.ToArray();
    }

    public static byte[] MemberReturnedFor(ushort memberId, ulong characterId, bool levelFlag)
    {
        var w = new PacketWriter(12);
        w.WriteUInt16(memberId);
        w.WriteUInt64(characterId);
        w.WriteUInt16(levelFlag ? (ushort)1 : (ushort)0);
        return w.ToArray();
    }

    public static byte[] DivisionFor(byte divisionType, IReadOnlyList<PartyDivisionShare> shares)
    {
        ArgumentNullException.ThrowIfNull(shares);
        var w = new PacketWriter(2 + shares.Count * 3);
        w.WriteByte(divisionType);
        if (divisionType is 2 or 3)
        {
            w.WriteByte((byte)shares.Count);
            foreach (var share in shares)
            {
                w.WriteUInt16(share.MemberId);
                w.WriteByte(share.Percent);
            }
        }
        else
        {
            w.WriteByte(0);
        }
        return w.ToArray();
    }

    public static byte[] ChatLineFor(ushort senderMemberId, string text, Encoding encoding)
    {
        var w = new PacketWriter(64);
        w.WriteUInt16(senderMemberId);
        w.WriteNulTerminated(text, encoding);
        return w.ToArray();
    }

    public static byte[] MasterChangedFor(ushort masterMemberId, bool levelFlag)
    {
        var w = new PacketWriter(3);
        w.WriteUInt16(masterMemberId);
        w.WriteByte(levelFlag ? (byte)1 : (byte)0);
        return w.ToArray();
    }

    public static byte[] MemberFlagFor(bool raised, ulong characterId)
    {
        var w = new PacketWriter(10);
        w.WriteUInt16(raised ? (ushort)1 : (ushort)0);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static byte[] JoinAskRelayFor(string askerName, ushort x, uint y, Encoding encoding)
    {
        var w = new PacketWriter(32);
        w.WriteNulTerminated(askerName, encoding);
        w.WriteUInt16(x);
        w.WriteUInt32(y);
        return w.ToArray();
    }

    public static byte[] CandidateHasPartyFor(
        ushort candidatePartyId, string candidateName, string masterName, ushort averageLevel,
        byte partyType, byte memberCount, byte divisionType, byte itemShare, Encoding encoding)
    {
        var w = new PacketWriter(64);
        w.WriteUInt16(candidatePartyId);
        w.WriteNulTerminated(candidateName, encoding);
        w.WriteNulTerminated(masterName, encoding);
        w.WriteUInt16(averageLevel);
        w.WriteByte(partyType);
        w.WriteByte(memberCount);
        w.WriteByte(divisionType);
        w.WriteByte(itemShare);
        return w.ToArray();
    }

    public static byte[] InfoFor(
        ushort partyId, string name, ushort averageLevel, byte partyType, byte memberCount,
        byte divisionType, byte itemShare, Encoding encoding)
    {
        var w = new PacketWriter(48);
        w.WriteUInt16(partyId);
        w.WriteNulTerminated(name, encoding);
        w.WriteUInt16(averageLevel);
        w.WriteByte(partyType);
        w.WriteByte(memberCount);
        w.WriteByte(divisionType);
        w.WriteByte(itemShare);
        return w.ToArray();
    }

    public static byte[] ZoneFor(ushort memberId, ushort zone) => MemberPair(memberId, zone);

    public static byte[] MemberNameFor(ushort memberId, string name, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(encoding);
        var w = new PacketWriter(24);
        w.WriteUInt16(memberId);
        w.WriteNulTerminated(name, encoding);
        return w.ToArray();
    }

    public static byte[] MemberStateFor(ushort memberId, byte kind, byte detail)
    {
        var w = new PacketWriter(4);
        w.WriteUInt16(memberId);
        w.WriteByte(kind);
        w.WriteByte(detail);
        return w.ToArray();
    }

    public static void WriteMemberInfo(PacketWriter w, PartyMemberInfo member, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(w);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(encoding);
        var name = new byte[MemberNameLength];
        var encoded = encoding.GetBytes(member.Name);
        if (encoded.Length >= MemberNameLength)
        {
            throw new ArgumentException($"Party member name '{member.Name}' does not fit {MemberNameLength - 1} bytes.", nameof(member));
        }
        encoded.CopyTo(name, 0);
        w.WriteBytes(name);
        w.WriteUInt16(member.Kind);
        w.WriteByte(0);
        w.WriteUInt16(member.Zone);
        w.WriteUInt16(member.MemberId);
        w.WriteZero(16);
        w.WriteUInt16(member.Level);
        w.WriteZero(6);
        w.WriteUInt64(member.CharacterId);
        w.WriteByte(0);
    }
}
