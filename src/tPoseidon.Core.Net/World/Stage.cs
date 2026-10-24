namespace tPoseidon.Core.Net.World;

public sealed class Stage
{
    public ushort MapId { get; init; }
    public string Md3FileName { get; init; } = string.Empty;
    public string LyrFileName { get; init; } = string.Empty;

    public bool IsLoaded { get; private set; }

    public void MarkLoaded()
    {
        IsLoaded = true;
    }
}
