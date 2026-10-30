namespace tPoseidon.Core.Game.Party;

public enum PartyFollowRefusal : byte
{
    DestIsTooFar = 0,
    NotAvailable = 1,
    IsNotVisible = 2,
    IsNotMyParty = 3,
    IsPortalLimit = 4,
    PortalClosed = 5,
    PortalNotAllowed = 7,
}

public enum PartyFollowStep
{
    Stop,
    Keep,
    Move,
}

public static class PartyFollowRules
{
    public const int StopDistance = 96;
    public const int PeriodMilliseconds = 500;

    public static readonly TimeSpan Period = TimeSpan.FromMilliseconds(PeriodMilliseconds);

    public static PartyFollowStep Decide(bool followerAlive, bool targetPresent, bool sameParty, double distanceSquared)
    {
        if (!followerAlive || !targetPresent || !sameParty) return PartyFollowStep.Stop;
        return distanceSquared > (double)StopDistance * StopDistance ? PartyFollowStep.Move : PartyFollowStep.Keep;
    }
}
