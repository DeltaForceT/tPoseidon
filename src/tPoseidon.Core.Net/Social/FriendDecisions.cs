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

public readonly record struct FriendZoneInfo(ushort First, ushort Second, bool InTransit)
{
    public static FriendZoneInfo Settled(ushort zone) => new(zone, zone, false);

    public static FriendZoneInfo Moving(ushort from, ushort to) => new(from, to, true);
}

public sealed record FriendPositionReply(ushort Opcode, byte[] Payload, bool TracksPosition);

public sealed record FriendZoneReply(ushort Opcode, byte[] Payload);

public enum ZoneMoveStep
{
    Answer,
    LocateFriend,
}

public sealed record ZoneMovePlan(ZoneMoveStep Step, ZoneMoveAnswer Answer);

public static class FriendDecisions
{
    public static AcceptClass ClassifyAccept(bool acceptorListsRequester, bool requesterListsAcceptor)
    {
        if (!acceptorListsRequester) return AcceptClass.Insert;
        return requesterListsAcceptor ? AcceptClass.AlreadyFriends : AcceptClass.PairMismatch;
    }

    public static ZoneMovePlan ZoneMove(FriendZoneInfo own, FriendZoneInfo friend)
    {
        if (friend.InTransit) return new ZoneMovePlan(ZoneMoveStep.Answer, ZoneMoveAnswer.MapMoving);
        return own.Second == friend.Second
            ? new ZoneMovePlan(ZoneMoveStep.Answer, ZoneMoveAnswer.SameZone)
            : new ZoneMovePlan(ZoneMoveStep.LocateFriend, ZoneMoveAnswer.NotReachable);
    }

    public static FriendPositionReply Position(FriendZoneInfo own, FriendZoneInfo friend)
    {
        if (friend.InTransit) return new FriendPositionReply(FriendFrames.PositionTransit, FriendFrames.ZonePair(friend.First, friend.Second), false);
        return new FriendPositionReply(FriendFrames.PositionZone, FriendFrames.Zone(friend.First), own.Second == friend.Second);
    }

    public static FriendZoneReply ZoneMoveFromLocation(ReadOnlySpan<byte> location)
    {
        if (!FriendFrames.TryReadLocation(location, out var found) || found.Zone == 0 || found.Zone > FriendFrames.LastMovableZone)
        {
            return new FriendZoneReply(FriendFrames.ChangeZoneToAnswer, FriendFrames.ZoneMoveAnswer(ZoneMoveAnswer.NotReachable));
        }
        return new FriendZoneReply(FriendFrames.ChangeZoneToPosition, FriendFrames.Location(found));
    }
}
