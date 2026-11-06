using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public sealed record LyrPortal(int DestinationMap, int X1, int Y1, int X2, int Y2);

public static class LyrPortalReader
{
    public const uint PackedLayerMagic = 0xC0D0;
    public const uint PackedBackgroundMagic = 0xA0B0;
    public const uint SpriteMagic = 0x49524F4E;
    public const int PackedHeaderBytes = 12;
    public const int LayerHeaderBytes = 68;
    public const int LayerBodySizeOffset = 56;
    public const int ObjectBytes = 56;
    public const int ObjectNameOffset = 16;
    public const int ObjectNameBytes = 32;
    public const int ObjectPortalFlagOffset = 48;
    public const int PortalFlag = 1;
    public const int MaxOffsetTableEntries = 200;
    public const int NameCodePage = 949;

    private const int SpriteAnimationCountOffset = 0x1C;
    private const int SpritePaletteFlagOffset = 0x38;
    private const int SpriteFrameCountOffset = 0x4C;
    private const int SpriteFirstChunkOffset = 0x54;
    private const int PaletteHeaderBytes = 0x20;
    private const int PaletteColourBytes = 768;
    private const int PaletteExtraFlagOffset = 0x18;
    private const int PaletteExtraBytes = 8;
    private const int ImageHeaderBytes = 24;
    private const int AnimationNameBytes = 32;
    private const int FrameLayerBytes = 28;

    public static IReadOnlyList<LyrPortal> Read(byte[] lyrFile, byte[] backgroundFile)
    {
        ArgumentNullException.ThrowIfNull(lyrFile);
        ArgumentNullException.ThrowIfNull(backgroundFile);
        var portals = new List<LyrPortal>();
        var layers = Unpack(lyrFile, PackedLayerMagic);
        var sprites = Unpack(backgroundFile, PackedBackgroundMagic);
        if (layers is null || sprites is null) return portals;
        var bounds = SpriteBounds(sprites);
        foreach (var layer in LayerOffsets(layers))
        {
            var bodySize = (int)ReadUInt32(layers, layer + LayerBodySizeOffset);
            var countAt = layer + LayerHeaderBytes + bodySize;
            var count = (int)ReadUInt32(layers, countAt);
            var first = countAt + 4;
            for (var index = 0; index < count; index++)
            {
                var at = first + index * ObjectBytes;
                if (ReadUInt32(layers, at + ObjectPortalFlagOffset) != PortalFlag) continue;
                var destination = (int)ReadUInt32(layers, at + ObjectPortalFlagOffset + 4);
                if (destination is <= 0 or > ushort.MaxValue) continue;
                if (!bounds.TryGetValue(ReadName(layers, at + ObjectNameOffset), out var box)) continue;
                var originX = BinaryPrimitives.ReadInt32LittleEndian(layers.AsSpan(at + 4, 4));
                var originY = BinaryPrimitives.ReadInt32LittleEndian(layers.AsSpan(at + 8, 4));
                portals.Add(new LyrPortal(destination, originX + box.X1, originY + box.Y1, originX + box.X2, originY + box.Y2));
            }
        }
        return portals;
    }

    private static byte[]? Unpack(byte[] file, uint magic)
    {
        if (file.Length < PackedHeaderBytes || ReadUInt32(file, 0) != magic) return file;
        using var zlib = new ZLibStream(new MemoryStream(file, PackedHeaderBytes, file.Length - PackedHeaderBytes), CompressionMode.Decompress);
        using var output = new MemoryStream();
        try
        {
            zlib.CopyTo(output);
        }
        catch (InvalidDataException)
        {
            return null;
        }
        return output.ToArray();
    }

    private static IReadOnlyList<int> LayerOffsets(byte[] data)
    {
        if (data.Length < 4 || ReadUInt32(data, 0) != 0) return Array.Empty<int>();
        for (var entries = 1; entries <= MaxOffsetTableEntries && entries * 4 <= data.Length; entries++)
        {
            var offsets = new uint[entries];
            for (var index = 0; index < entries; index++) offsets[index] = ReadUInt32(data, index * 4);
            var ascending = true;
            for (var index = 1; index < entries && ascending; index++) ascending = offsets[index - 1] < offsets[index];
            if (!ascending) continue;
            var tableEnd = entries * 4;
            var blocks = new List<int>(entries);
            var consistent = true;
            for (var index = 0; index < entries && consistent; index++)
            {
                var at = (long)tableEnd + offsets[index];
                if (at + LayerHeaderBytes + 4 > data.Length) { consistent = false; break; }
                var bodySize = ReadUInt32(data, (int)at + LayerBodySizeOffset);
                var countAt = at + LayerHeaderBytes + bodySize;
                if (countAt + 4 > data.Length) { consistent = false; break; }
                var count = ReadUInt32(data, (int)countAt);
                var end = countAt + 4 + (long)count * ObjectBytes;
                var next = index + 1 < entries ? tableEnd + offsets[index + 1] : data.Length;
                consistent = end == next;
                blocks.Add((int)at);
            }
            if (consistent) return blocks;
        }
        return Array.Empty<int>();
    }

    private static Dictionary<string, (int X1, int Y1, int X2, int Y2)> SpriteBounds(byte[] sprites)
    {
        var result = new Dictionary<string, (int X1, int Y1, int X2, int Y2)>(StringComparer.Ordinal);
        if (sprites.Length < SpriteFirstChunkOffset + 4 || ReadUInt32(sprites, 0) != SpriteMagic) return result;
        var animations = (int)ReadUInt32(sprites, SpriteAnimationCountOffset);
        var frames = (int)ReadUInt32(sprites, SpriteFrameCountOffset);
        var position = SpriteFirstChunkOffset;
        if (ReadUInt32(sprites, SpritePaletteFlagOffset) == 1)
        {
            var extra = ReadUInt32(sprites, position + PaletteExtraFlagOffset) == 1 ? PaletteExtraBytes : 0;
            position += PaletteHeaderBytes + PaletteColourBytes + extra;
        }
        position += frames * 4;
        var sizes = new List<(int Width, int Height)>();
        for (var frame = 0; frame < frames; frame++)
        {
            if (position + 4 > sprites.Length) return result;
            var images = (int)ReadUInt32(sprites, position);
            position += 4;
            for (var image = 0; image < images; image++)
            {
                if (position + ImageHeaderBytes > sprites.Length) return result;
                var dataSize = (int)ReadUInt32(sprites, position);
                sizes.Add(((int)ReadUInt32(sprites, position + 4), (int)ReadUInt32(sprites, position + 8)));
                position += ImageHeaderBytes + dataSize;
            }
        }
        var tableEnd = position + animations * 4;
        for (var animation = 0; animation < animations; animation++)
        {
            if (position + animation * 4 + 4 > sprites.Length) break;
            var at = tableEnd + (int)ReadUInt32(sprites, position + animation * 4);
            if (at + AnimationNameBytes + 8 > sprites.Length) continue;
            var name = ReadName(sprites, at);
            var frameCount = (int)ReadUInt32(sprites, at + AnimationNameBytes);
            if (frameCount < 1) continue;
            var frameAt = at + AnimationNameBytes + 4 + frameCount * 4 + (int)ReadUInt32(sprites, at + AnimationNameBytes + 4);
            if (frameAt + 8 > sprites.Length) continue;
            var layerCount = (int)ReadUInt32(sprites, frameAt + 4);
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (var layer = 0; layer < layerCount; layer++)
            {
                var layerAt = frameAt + 8 + layer * FrameLayerBytes;
                if (layerAt + FrameLayerBytes > sprites.Length) break;
                var image = (int)ReadUInt32(sprites, layerAt);
                if (image < 0 || image >= sizes.Count) continue;
                var x = BinaryPrimitives.ReadInt32LittleEndian(sprites.AsSpan(layerAt + 4, 4));
                var y = BinaryPrimitives.ReadInt32LittleEndian(sprites.AsSpan(layerAt + 8, 4));
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x + sizes[image].Width);
                maxY = Math.Max(maxY, y + sizes[image].Height);
            }
            if (minX != int.MaxValue) result.TryAdd(name, (minX, minY, maxX, maxY));
        }
        return result;
    }

    private static string ReadName(byte[] data, int at)
    {
        var room = Math.Min(ObjectNameBytes, data.Length - at);
        var end = Array.IndexOf(data, (byte)0, at, room);
        return CodePagesEncodingProvider.Instance.GetEncoding(NameCodePage)!.GetString(data, at, (end < 0 ? room : end - at));
    }

    private static uint ReadUInt32(byte[] data, int at) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at, 4));
}
