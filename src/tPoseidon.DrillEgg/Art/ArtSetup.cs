namespace tPoseidon.DrillEgg.Art;

internal enum ArtState
{
    Ready,
    Missing,
    Failed,
}

internal delegate int ArtExtractor(string swf, string outputFolder);

internal sealed class ArtSetup
{
    public const string LibraryFile = "library.json";
    public const string PartialSuffix = ".partial";

    private readonly ArtExtractor _extract;
    private readonly TextWriter _log;
    private readonly object _gate = new();

    public ArtSetup(string artFolder, string swfPath, ArtExtractor extract, TextWriter log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(swfPath);
        ArtFolder = Path.GetFullPath(artFolder);
        SwfPath = Path.GetFullPath(swfPath);
        _extract = extract ?? throw new ArgumentNullException(nameof(extract));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public string ArtFolder { get; }

    public string SwfPath { get; }

    public ArtState State { get; private set; } = ArtState.Missing;

    public string? Problem { get; private set; }

    public ArtState Prepare()
    {
        lock (_gate)
        {
            if (File.Exists(Path.Combine(ArtFolder, LibraryFile)))
            {
                Problem = null;
                return State = ArtState.Ready;
            }
            if (!File.Exists(SwfPath))
            {
                Problem = null;
                return State = ArtState.Missing;
            }
            if (Directory.Exists(ArtFolder) && Directory.EnumerateFileSystemEntries(ArtFolder).Any())
            {
                Problem = $"The folder {ArtFolder} already exists but has no {LibraryFile}. Delete or rename that folder so the game art can be made again.";
                return State = ArtState.Failed;
            }
            var partial = ArtFolder + PartialSuffix;
            if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
            _log.WriteLine($"[drill-egg] making the game art from {SwfPath}");
            var code = _extract(SwfPath, partial);
            if (code != 0 || !File.Exists(Path.Combine(partial, LibraryFile)))
            {
                if (Directory.Exists(partial)) Directory.Delete(partial, recursive: true);
                Problem = $"{Path.GetFileName(SwfPath)} could not be read. Make sure it is the game's own myshop.swf file; the window where the server runs shows the details.";
                return State = ArtState.Failed;
            }
            if (Directory.Exists(ArtFolder)) Directory.Delete(ArtFolder);
            Directory.Move(partial, ArtFolder);
            _log.WriteLine($"[drill-egg] game art is ready in {ArtFolder}");
            Problem = null;
            return State = ArtState.Ready;
        }
    }
}
