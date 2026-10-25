namespace tPoseidon.Core.Net.Social;

public enum AcceptClass
{
    Insert,
    AlreadyFriends,
    PairMismatch,
}

public enum ZoneMoveAnswer : byte
{
    MapMoving = 0,
    SameZone = 1,
    NotReachable = 2,
}

public static class FriendDecisions
{
    public static AcceptClass ClassifyAccept(bool acceptorListsRequester, bool requesterListsAcceptor)
    {
        if (!acceptorListsRequester) return AcceptClass.Insert;
        return requesterListsAcceptor ? AcceptClass.AlreadyFriends : AcceptClass.PairMismatch;
    }

    public static ZoneMoveAnswer ZoneMove(ushort? ownZone, ushort friendZone) =>
        ownZone == friendZone ? ZoneMoveAnswer.SameZone : ZoneMoveAnswer.NotReachable;
}
