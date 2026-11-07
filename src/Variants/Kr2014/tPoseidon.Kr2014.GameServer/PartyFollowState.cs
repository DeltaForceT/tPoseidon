internal sealed class PartyFollowState(ushort targetEntityId, ulong targetCharacterId)
{
    public ushort TargetEntityId { get; } = targetEntityId;

    public ulong TargetCharacterId { get; } = targetCharacterId;

    public DateTimeOffset NextStepAt { get; set; }

    public (int X, int Y)? Destination { get; set; }

    public DateTimeOffset PortalPendingUntil { get; set; }
}
