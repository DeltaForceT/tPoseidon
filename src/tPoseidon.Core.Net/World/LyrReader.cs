namespace tPoseidon.Core.Net.World;

public sealed class LyrReader
{
    public sealed record GroundAttributeMap(int Width, int Height, byte[] Tiles)
    {
        public bool IsWalkable(int tx, int ty)
        {
            if (tx < 0 || tx >= Width) return false;
            if (ty < 0 || ty >= Height) return false;
            return (Tiles[ty * Width + tx] & 0x01) != 0;
        }
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

    public static GroundAttributeMap Synthetic(int width, int height, bool walkable = true)
    {
        var tiles = new byte[width * height];
        if (walkable) Array.Fill(tiles, (byte)0x01);
        return new GroundAttributeMap(width, height, tiles);
    }
}
