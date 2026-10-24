using tPoseidon.Core.Common;
using tPoseidon.Gacha.Art;

namespace tPoseidon.Tools.ReleasePack;

internal sealed class DrillEggPackageBuilder : IDisposable
{
    public const string SwfName = "myshop.swf";
    public const string ArtFolder = "art";
    public const string WithArtSuffix = "-with-art";

    private const string Project = "src/tPoseidon.DrillEgg/tPoseidon.DrillEgg.csproj";
    private const string PackFolder = "src/tPoseidon.DrillEgg/pack";
    private const string ExamplesFolder = "examples";
    private const string PicturesFolder = "pictures";
    private const string WindowsStart = "start.cmd";
    private const string UnixStart = "start.sh";

    private static readonly string[] PackFiles = ["README.txt", "drill-egg.json"];

    private readonly string _repoRoot;
    private readonly PackOptions _options;
    private readonly ReleaseLock _lock;
    private readonly string _stageRoot;
    private readonly string _cache;
    private readonly BuildIdentity _identity;
    private List<(string Source, string Entry)>? _licenses;
    private string? _art;

    public DrillEggPackageBuilder(string repoRoot, PackOptions options, ReleaseLock releaseLock)
    {
        _repoRoot = repoRoot;
        _options = options;
        _lock = releaseLock;
        Output = Path.GetFullPath(options.Output ?? Path.Combine(repoRoot, "artifacts", "release"));
        _stageRoot = Path.Combine(repoRoot, "artifacts", "stage");
        _cache = Path.GetFullPath(options.Cache ?? Path.Combine(repoRoot, "artifacts", "cache"));
        var commit = PackageTools.GitHead(repoRoot);
        _identity = new BuildIdentity(options.Version, commit, BuildIdentity.ComputeTag(options.Version, commit));
    }

    public BuildIdentity Identity => _identity;

    public void Dispose()
    {
        if (_art is not null && Directory.Exists(_art))
        {
            Directory.Delete(_art, recursive: true);
        }
    }

    public string Output { get; }

    public async Task<IReadOnlyList<PackageResult>> BuildAsync(string rid, CancellationToken ct)
    {
        var name = $"{ReleaseManifest.DrillEggFilePrefix}-{_options.Version}-{rid}";
        var stage = Path.Combine(_stageRoot, name);
        if (Directory.Exists(stage))
        {
            Directory.Delete(stage, recursive: true);
        }

        var app = Path.Combine(stage, "app");
        Directory.CreateDirectory(app);
        Console.WriteLine($"{rid}: publishing the Drill Egg");
        PackageTools.Publish(_repoRoot, Project, rid, app, _identity, []);
        _licenses ??= await PackageTools.LicensesAsync(_lock.DrillEggLicenses, _cache, ct).ConfigureAwait(false);
        Directory.CreateDirectory(Output);

        var packages = new List<PackageResult> { await WriteAsync(name, stage, rid, null, ct).ConfigureAwait(false) };
        if (_options.ArtSwf is { } swf)
        {
            _art ??= ExtractArt(swf);
            packages.Add(await WriteAsync(name + WithArtSuffix, stage, rid, (swf, _art), ct).ConfigureAwait(false));
        }

        return packages;
    }

    private async Task<PackageResult> WriteAsync(string name, string stage, string rid, (string Swf, string Art)? original, CancellationToken ct)
    {
        var archivePath = Path.Combine(Output, name + ".zip");
        IReadOnlyList<PackageEntry> entries;
        var pack = Path.Combine(_repoRoot, PackFolder);
        using (var archive = new PackageArchive(archivePath, zip: true, comment: $"{ReleaseManifest.DrillEggProduct} {_identity}"))
        {
            foreach (var file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                archive.AddFile(file, name + "/" + Path.GetRelativePath(stage, file).Replace('\\', '/'), PackageTools.ModeFor(file));
            }

            foreach (var file in PackFiles)
            {
                archive.AddFile(Path.Combine(pack, file), name + "/" + file, PackageArchive.RegularMode);
            }

            foreach (var example in Directory.EnumerateFiles(Path.Combine(pack, ExamplesFolder), "*.json").Order(StringComparer.Ordinal))
            {
                archive.AddFile(example, $"{name}/{ExamplesFolder}/{Path.GetFileName(example)}", PackageArchive.RegularMode);
            }

            var windows = rid.StartsWith("win-", StringComparison.Ordinal);
            var start = windows ? WindowsStart : UnixStart;
            archive.AddFile(Path.Combine(pack, start), name + "/" + start, windows ? PackageArchive.RegularMode : PackageArchive.ExecutableMode);
            archive.AddFile(Path.Combine(_repoRoot, "LICENSE"), name + "/LICENSE.txt", PackageArchive.RegularMode);
            foreach (var (source, entry) in _licenses!)
            {
                archive.AddFile(source, name + "/" + entry, PackageArchive.RegularMode);
            }

            archive.AddDirectory(name + "/" + PicturesFolder, PackageArchive.ExecutableMode);
            if (original is { } files)
            {
                archive.AddFile(files.Swf, name + "/" + SwfName, PackageArchive.RegularMode);
                foreach (var file in Directory.EnumerateFiles(files.Art, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
                {
                    archive.AddFile(file, $"{name}/{ArtFolder}/{Path.GetRelativePath(files.Art, file).Replace('\\', '/')}", PackageArchive.RegularMode);
                }
            }

            entries = archive.Entries;
        }

        if (original is null)
        {
            var foreign = entries.FirstOrDefault(entry => entry.Path.EndsWith(".swf", StringComparison.OrdinalIgnoreCase) || entry.Path.StartsWith($"{name}/{ArtFolder}/", StringComparison.Ordinal));
            if (foreign is not null)
            {
                File.Delete(archivePath);
                throw new InvalidOperationException($"{foreign.Path} is game art and must not be in the clean pack {Path.GetFileName(archivePath)}.");
            }
        }

        return new PackageResult(archivePath, new FileInfo(archivePath).Length, await PackageTools.HashOfAsync(archivePath, ct).ConfigureAwait(false), entries);
    }

    private static string ExtractArt(string swf)
    {
        var art = Path.Combine(Path.GetTempPath(), "tposeidon-drill-egg-art-" + Guid.NewGuid().ToString("N"));

        Console.WriteLine($"making the game art from {swf} for the private with-art packs");
        if (ExtractCommand.Run(swf, art, Console.Out, Console.Error) != ExtractCommand.Succeeded)
        {
            if (Directory.Exists(art))
            {
                Directory.Delete(art, recursive: true);
            }

            throw new InvalidDataException($"{swf} could not be turned into game art.");
        }

        return art;
    }
}
