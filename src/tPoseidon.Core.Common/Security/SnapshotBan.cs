namespace tPoseidon.Core.Common.Security;

public sealed class SnapshotBan
{
    public string Address { get; set; } = "";

    public DateTimeOffset Since { get; set; }

    public DateTimeOffset Until { get; set; }

    public int Level { get; set; }

    public string Reason { get; set; } = "";
}
