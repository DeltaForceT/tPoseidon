internal sealed class PartyFollowState(ushort targetEntityId)
{
    public ushort TargetEntityId { get; } = targetEntityId;

    public DateTimeOffset NextStepAt { get; set; }

    public (int X, int Y)? Destination { get; set; }
}
