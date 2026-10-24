using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed record FriendListed(FriendEntry Entry, bool Online);

public static class FriendFrames
{
    public const ushort SessionOpen = 0x2135;
    public const ushort Logout = 0x2136;
    public const ushort AddRequest = 0x2137;
    public const ushort Delete = 0x2138;
    public const ushort DeleteOffline = 0x2139;
    public const ushort Accept = 0x213A;
    public const ushort Refuse = 0x213B;
    public const ushort CancelRequest = 0x2147;
    public const ushort ChangeGroup = 0x215A;
    public const ushort RenameGroup = 0x215D;
    public const ushort OnlineMessage = 0x216C;
    public const ushort OfflineMessage = 0x2171;
    public const ushort RequestOption = 0x215F;

    public const ushort NotAvailable = 0x213C;
    public const ushort NotFound = 0x213D;
    public const ushort NotOnline = 0x2141;
    public const ushort AddedForRequester = 0x2142;
    public const ushort RequestReceived = 0x2144;
    public const ushort AddedForAcceptor = 0x2146;
    public const ushort AlreadyFriends = 0x2151;
    public const ushort RemoveStarted = 0x2156;
    public const ushort RequestsRefused = 0x2160;
    public const ushort CounterpartGone = 0x2161;
    public const ushort RemovedWhileOffline = 0x2162;
    public const ushort RefusalSent = 0x2163;
    public const ushort RequestSent = 0x2164;
    public const ushort MessageTargetUnknown = 0x216D;
    public const ushort MessageTargetNotReady = 0x216E;
    public const ushort MessageNotMyFriend = 0x2175;
    public const ushort MessageNotTheirFriend = 0x2176;
    public const ushort RemoveFailed = 0x2184;
    public const ushort AddFailed = 0x2187;
    public const ushort ListWithFriends = 0x213E;
    public const ushort FriendOnline = 0x213F;
    public const ushort FriendOffline = 0x2140;
    public const ushort RequestRefused = 0x2143;
    public const ushort RequestCancelled = 0x2148;
    public const ushort RequestedSelf = 0x214D;
    public const ushort RemovedByOwner = 0x2153;
    public const ushort RemovedFriend = 0x2154;
    public const ushort RemovedOffline = 0x2155;
    public const ushort ListWithoutFriends = 0x2159;
    public const ushort GroupChanged = 0x215B;
    public const ushort GroupChangeRefused = 0x215C;
    public const ushort OwnListFull = 0x215E;
    public const ushort TargetListFull = 0x2166;
    public const ushort MessageDelivered = 0x216F;
    public const ushort MessageSentTo = 0x2170;
    public const ushort OfflineMessagesFull = 0x2172;
    public const ushort OfflineMessageStored = 0x2173;
    public const ushort OfflineMessageFailed = 0x2174;

    public const byte OnlineStatus = 1;
    public const byte OfflineStatus = 2;
    public const byte NotAvailableReason = 0x0F;
    public const uint GroupChangeRefusedCode = 10;

    public static byte[] List(
        IReadOnlyList<string> removedBy,
        IReadOnlyList<FriendGroup> groups,
        IReadOnlyList<FriendListed> friends,
        IReadOnlyList<FriendMessage> messages,
        Encoding text)
    {
        ArgumentNullException.ThrowIfNull(removedBy);
        ArgumentNullException.ThrowIfNull(groups);
        ArgumentNullException.ThrowIfNull(friends);
        ArgumentNullException.ThrowIfNull(messages);
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(256);
        w.WriteUInt32((uint)removedBy.Count);
        foreach (var name in removedBy) w.WriteNulTerminated(name, text);
        w.WriteUInt32((uint)groups.Count);
        foreach (var group in groups)
        {
            w.WriteUInt32(group.GroupNum);
            w.WriteNulTerminated(group.GroupName, text);
        }
        if (friends.Count > 0)
        {
            w.WriteUInt32((uint)friends.Count);
            foreach (var friend in friends)
            {
                w.WriteUInt64(friend.Entry.FriendCharacterId);
                w.WriteByte(friend.Entry.GroupNum);
                w.WriteNulTerminated(friend.Entry.Name, text);
                w.WriteByte(friend.Online ? OnlineStatus : OfflineStatus);
                w.WriteUInt16(friend.Entry.CharacterType);
            }
        }
        w.WriteUInt32((uint)messages.Count);
        foreach (var message in messages)
        {
            w.WriteUInt64(message.SenderCharacterId);
            w.WriteNulTerminated(message.SentAt.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), text);
            w.WriteNulTerminated(message.Content, text);
            w.WriteNulTerminated(message.SenderName, text);
            w.WriteUInt32(message.Kind);
        }
        return w.ToArray();
    }

    public static ushort ListOpcode(int friendCount) => friendCount > 0 ? ListWithFriends : ListWithoutFriends;

    public static byte[] Character(ulong characterId)
    {
        var w = new PacketWriter(8);
        w.WriteUInt64(characterId);
        return w.ToArray();
    }

    public static byte[] Name(string name, Encoding text)
    {
        var w = new PacketWriter(24);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] CharacterAndName(ulong characterId, string name, Encoding text)
    {
        var w = new PacketWriter(32);
        w.WriteUInt64(characterId);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] RemovedOfflineResult(ulong characterId, string name, Encoding text)
    {
        var w = new PacketWriter(40);
        w.WriteUInt64(characterId);
        w.WriteNulTerminated(name, text);
        return w.ToArray();
    }

    public static byte[] GroupChange(ulong friendCharacterId, byte groupNum, uint value)
    {
        var w = new PacketWriter(13);
        w.WriteUInt64(friendCharacterId);
        w.WriteByte(groupNum);
        w.WriteUInt32(value);
        return w.ToArray();
    }

    public static byte[] Unavailable() => new[] { NotAvailableReason };

    public static byte[] AddedEntry(ulong characterId, byte groupNum, string name, ushort characterType, bool forRequester, Encoding text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var w = new PacketWriter(48);
        w.WriteUInt64(characterId);
        w.WriteByte(groupNum);
        w.WriteNulTerminated(name, text);
        if (forRequester) w.WriteByte(OnlineStatus);
        w.WriteUInt16(characterType);
        return w.ToArray();
    }

    public static byte[] MessageFrom(ulong senderCharacterId, string content, Encoding text)
    {
        var w = new PacketWriter(64);
        w.WriteUInt64(senderCharacterId);
        w.WriteNulTerminated(content, text);
        return w.ToArray();
    }
}
