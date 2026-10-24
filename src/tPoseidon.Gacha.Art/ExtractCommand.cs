using tPoseidon.Gacha.Art.Images;
using tPoseidon.Gacha.Art.Output;
using tPoseidon.Gacha.Art.Swf;

namespace tPoseidon.Gacha.Art;

public static class ExtractCommand
{
    public const int Succeeded = 0;
    public const int Failed = 1;
    public const int BadArguments = 2;

    private const string LogTag = "[gacha-art]";

    public static int Run(string source, string output, TextWriter log, TextWriter errors)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(errors);
        if (!File.Exists(source))
        {
            errors.WriteLine($"{LogTag} {source} does not exist");
            return BadArguments;
        }
        try
        {
            var summary = ArtExtractor.Extract(File.ReadAllBytes(source), output);
            log.WriteLine($"{LogTag} wrote {summary.Bitmaps} bitmap(s), {summary.Sounds} sound(s), {summary.Shapes} shape(s), {summary.Sprites} sprite(s), {summary.Buttons} button(s), {summary.Texts} text field(s) to {Path.GetFullPath(output)}");
            foreach (var skipped in summary.Skipped) log.WriteLine($"{LogTag} skipped {skipped.Kind} {skipped.Id}: {skipped.Reason}");
            return Succeeded;
        }
        catch (SwfFormatException e)
        {
            errors.WriteLine($"{LogTag} {source} is not a readable movie: {e.Message}");
            return Failed;
        }
        catch (InvalidDataException e)
        {
            errors.WriteLine($"{LogTag} {source} holds damaged compressed data: {e.Message}");
            return Failed;
        }
        catch (JpegFormatException e)
        {
            errors.WriteLine($"{LogTag} a bitmap in {source} could not be decoded: {e.Message}");
            return Failed;
        }
        catch (IOException e)
        {
            errors.WriteLine($"{LogTag} could not write to {output}: {e.Message}");
            return Failed;
        }
        catch (UnauthorizedAccessException e)
        {
            errors.WriteLine($"{LogTag} could not write to {output}: {e.Message}");
            return Failed;
        }
    }
}
