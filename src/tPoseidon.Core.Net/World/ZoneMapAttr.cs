namespace tPoseidon.Core.Net.World;

public sealed class ZoneMapAttr
{
    public ushort MapId { get; init; }
    public byte[] Bitmap { get; private set; } = Array.Empty<byte>();

    public void LoadBitmap(byte[] bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        Bitmap = bitmap;
    }
}
