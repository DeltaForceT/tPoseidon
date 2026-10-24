using System.Globalization;
using tPoseidon.Gacha.Art.Images;
using tPoseidon.Gacha.Art.Sounds;
using tPoseidon.Gacha.Art.Swf;

namespace tPoseidon.Gacha.Art.Output;

public sealed record ExtractionSummary(int Bitmaps, int Sounds, int Shapes, int Sprites, int Buttons, int Texts, IReadOnlyList<SkippedEntry> Skipped);

public static class ArtExtractor
{
    public const string LibraryFile = "library.json";
    public const string BitmapFolder = "bitmaps";
    public const string SoundFolder = "sounds";

    public static ExtractionSummary Extract(byte[] swf, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(swf);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        var library = SwfLibrary.Load(SwfFile.Parse(swf));
        Directory.CreateDirectory(Path.Combine(outputDirectory, BitmapFolder));
        Directory.CreateDirectory(Path.Combine(outputDirectory, SoundFolder));
        var skipped = new List<SkippedEntry>();
        var bitmaps = new List<BitmapEntry>();
        foreach (var definition in library.Bitmaps.Values.OrderBy(b => b.Id))
        {
            var bitmap = BitmapExporter.Export(library, definition);
            var name = $"{BitmapFolder}/{bitmap.Id.ToString(CultureInfo.InvariantCulture)}.{bitmap.Extension}";
            File.WriteAllBytes(Path.Combine(outputDirectory, name), bitmap.Content);
            bitmaps.Add(new BitmapEntry(bitmap.Id, name, bitmap.Width, bitmap.Height, bitmap.Alpha));
        }
        var sounds = new List<SoundEntry>();
        foreach (var definition in library.Sounds.Values.OrderBy(s => s.Id))
        {
            if (!SoundExporter.IsSupported(definition))
            {
                skipped.Add(new SkippedEntry(definition.Id, "sound", $"sound format {definition.Format.ToString(CultureInfo.InvariantCulture)} is not exported"));
                continue;
            }
            var sound = SoundExporter.Export(library, definition);
            var name = $"{SoundFolder}/{sound.Id.ToString(CultureInfo.InvariantCulture)}.{sound.Extension}";
            File.WriteAllBytes(Path.Combine(outputDirectory, name), sound.Content);
            sounds.Add(new SoundEntry(sound.Id, name, sound.SampleRate, sound.Channels, sound.SampleCount));
        }
        var json = new LibraryWriter(library, bitmaps, sounds, skipped).Write();
        File.WriteAllBytes(Path.Combine(outputDirectory, LibraryFile), json);
        return new ExtractionSummary(bitmaps.Count, sounds.Count, library.Shapes.Count, library.Sprites.Count, library.Buttons.Count, library.Texts.Count + library.EditTexts.Count, skipped);
    }
}
