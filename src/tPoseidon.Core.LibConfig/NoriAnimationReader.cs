using System.Buffers.Binary;
using System.Text;

namespace tPoseidon.Core.LibConfig;

public readonly record struct NoriAnimation(string Name, int FrameCount, int TotalMs);

public static class NoriAnimationReader
{
    public const int NameCodePage = 949;

    private const int NoriSignature = 0x49524F4E;
    private const int GawiSignature = 0x49574147;
    private const int PaletteSignature = 0x5F4C4150;

    private const int NoriHeaderSize = 40;
    private const int NoriAnimationCountOffset = 0x1C;
    private const int GawiHeaderSize = 44;
    private const int GawiFlagsOffset = 0x10;
    private const int GawiImageCountOffset = 0x24;
    private const int GawiPaletteFlag = 1;
    private const int PaletteHeaderSize = 32;
    private const int PaletteColorBytes = 768;
    private const int PalettePairCountOffset = 0x18;
    private const int PalettePairSize = 8;
    private const int SubImageHeaderSize = 24;
    private const int AnimationNameSize = 32;
    private const int OffsetSize = 4;

    public static IReadOnlyList<NoriAnimation> Read(ReadOnlySpan<byte> data, Encoding nameEncoding)
    {
        ArgumentNullException.ThrowIfNull(nameEncoding);
        if (data.Length < NoriHeaderSize + GawiHeaderSize
            || Int(data, 0) != NoriSignature
            || Int(data, NoriHeaderSize) != GawiSignature)
        {
            throw new InvalidDataException("Not a NORI sprite file.");
        }

        var animationCount = Int(data, NoriAnimationCountOffset);
        var flags = Int(data, NoriHeaderSize + GawiFlagsOffset);
        var imageCount = Int(data, NoriHeaderSize + GawiImageCountOffset);

        var imageTable = NoriHeaderSize + GawiHeaderSize;
        if ((flags & GawiPaletteFlag) != 0)
        {
            if (Int(data, imageTable) != PaletteSignature)
            {
                throw new InvalidDataException("NORI palette header missing.");
            }
            imageTable += PaletteHeaderSize + PaletteColorBytes
                + Int(data, imageTable + PalettePairCountOffset) * PalettePairSize;
        }

        var imageBase = imageTable + imageCount * OffsetSize;
        var animationTable = imageBase;
        for (var i = 0; i < imageCount; i++)
        {
            var cursor = imageBase + Int(data, imageTable + i * OffsetSize);
            var subImageCount = Int(data, cursor);
            cursor += OffsetSize;
            for (var s = 0; s < subImageCount; s++)
            {
                cursor += SubImageHeaderSize + Int(data, cursor);
            }
            animationTable = Math.Max(animationTable, cursor);
        }

        var animationBase = animationTable + animationCount * OffsetSize;
        var animations = new NoriAnimation[animationCount];
        for (var i = 0; i < animationCount; i++)
        {
            var start = animationBase + Int(data, animationTable + i * OffsetSize);
            var rawName = data.Slice(start, AnimationNameSize);
            var terminator = rawName.IndexOf((byte)0);
            var name = nameEncoding.GetString(terminator < 0 ? rawName : rawName[..terminator]);
            var frameCount = Int(data, start + AnimationNameSize);
            var frameTable = start + AnimationNameSize + OffsetSize;
            var frameBase = frameTable + frameCount * OffsetSize;
            var total = 0;
            for (var f = 0; f < frameCount; f++)
            {
                total += Int(data, frameBase + Int(data, frameTable + f * OffsetSize));
            }
            animations[i] = new NoriAnimation(name, frameCount, total);
        }
        return animations;
    }

    public static int? TotalOf(IReadOnlyList<NoriAnimation> animations, string name)
    {
        ArgumentNullException.ThrowIfNull(animations);
        ArgumentException.ThrowIfNullOrEmpty(name);
        var withDirection = name + "_";
        foreach (var animation in animations)
        {
            if (animation.Name == name || animation.Name.StartsWith(withDirection, StringComparison.Ordinal))
            {
                return animation.TotalMs;
            }
        }
        return null;
    }

    private static int Int(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, OffsetSize));
}
