using System.Text;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.Social;

public sealed class FriendOfflineCoupleMemos : ICoupleOfflineMemos
{
    private readonly IFriendStore _friends;
    private readonly int _limit;
    private readonly Encoding _text;

    public FriendOfflineCoupleMemos(IFriendStore friends, int limit, Encoding text)
    {
        _friends = friends ?? throw new ArgumentNullException(nameof(friends));
        _limit = limit;
        _text = text ?? throw new ArgumentNullException(nameof(text));
    }

    public bool Leave(ulong ownerCharacterId, ulong senderCharacterId, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_friends.CountMessages(ownerCharacterId) >= _limit) return false;
        return _friends.SendMessage(ownerCharacterId, senderCharacterId, 0, FriendRules.Truncate(text, FriendRules.MessageContentLength, _text));
    }
}
