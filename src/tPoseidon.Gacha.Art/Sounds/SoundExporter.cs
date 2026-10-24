using System.Buffers.Binary;
using System.Text;
using tPoseidon.Gacha.Art.Swf;

namespace tPoseidon.Gacha.Art.Sounds;

public sealed record ExportedSound(int Id, string Extension, byte[] Content, int SampleRate, int Channels, uint SampleCount);

public static class SoundExporter
{
    public const int UncompressedNativeFormat = 0;
    public const int Mp3Format = 2;
    public const int UncompressedLittleEndianFormat = 3;

    private static readonly int[] Rates = [5512, 11025, 22050, 44100];

    public static bool IsSupported(SoundDefinition sound)
    {
        ArgumentNullException.ThrowIfNull(sound);
        return sound.Format is Mp3Format or UncompressedNativeFormat or UncompressedLittleEndianFormat;
    }

    public static ExportedSound Export(SwfLibrary library, SoundDefinition sound)
    {
        ArgumentNullException.ThrowIfNull(library);
        ArgumentNullException.ThrowIfNull(sound);
        var data = library.File.Data.AsSpan(sound.DataOffset, sound.DataLength);
        var rate = Rates[sound.Rate];
        var channels = sound.Stereo ? 2 : 1;
        switch (sound.Format)
        {
            case Mp3Format:
                if (data.Length < 2) throw new SwfFormatException($"sound {sound.Id}: MP3 data too short");
                return new ExportedSound(sound.Id, "mp3", data[2..].ToArray(), rate, channels, sound.SampleCount);
            case UncompressedNativeFormat:
            case UncompressedLittleEndianFormat:
                return new ExportedSound(sound.Id, "wav", Wave(data, rate, channels, sound.SixteenBit), rate, channels, sound.SampleCount);
            default:
                throw new SwfFormatException($"sound {sound.Id}: format {sound.Format} is not supported");
        }
    }

    private static byte[] Wave(ReadOnlySpan<byte> samples, int rate, int channels, bool sixteenBit)
    {
        var bits = sixteenBit ? 16 : 8;
        var blockAlign = channels * bits / 8;
        var output = new byte[44 + samples.Length];
        Encoding.ASCII.GetBytes("RIFF").CopyTo(output, 0);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(4), 36 + samples.Length);
        Encoding.ASCII.GetBytes("WAVEfmt ").CopyTo(output, 8);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(16), 16);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(20), 1);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(22), (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(24), rate);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(28), rate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(32), (short)blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(34), (short)bits);
        Encoding.ASCII.GetBytes("data").CopyTo(output, 36);
        BinaryPrimitives.WriteInt32LittleEndian(output.AsSpan(40), samples.Length);
        samples.CopyTo(output.AsSpan(44));
        return output;
    }
}
