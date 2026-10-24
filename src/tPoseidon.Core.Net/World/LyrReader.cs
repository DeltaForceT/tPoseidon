namespace tPoseidon.Core.Net.World;

public sealed class LyrReader
{
    public sealed record GroundAttributeMap(int Width, int Height, byte[] Tiles)
    {
    }

    public static GroundAttributeMap? TryLoad(string lyrPath)
    {
        if (tPoseidon.Core.Common.ClientFiles.Locate(lyrPath) is not { } found) return null;
        try
        {
            var bytes = File.ReadAllBytes(found);
            if (bytes.Length < 8) return null;
            var width = BitConverter.ToInt32(bytes, 0);
            var height = BitConverter.ToInt32(bytes, 4);
            if (width <= 0 || height <= 0) return null;
            var expected = width * height;
            if (bytes.Length < 8 + expected) return null;
            var tiles = new byte[expected];
            Array.Copy(bytes, 8, tiles, 0, expected);
            return new GroundAttributeMap(width, height, tiles);
        }
        catch
        {
            return null;
        }
    }
}
