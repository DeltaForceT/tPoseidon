using tPoseidon.Core.Common;

namespace tPoseidon.Tools.ReleasePack;

internal sealed class PackageBuilder
{
    private static readonly string[] PublishedProjects =
    {
        "src/tPoseidon.Cli/tPoseidon.Cli.csproj",
        "src/tPoseidon.Panel/tPoseidon.Panel.csproj",
        "src/Variants/Kr2014/tPoseidon.Kr2014.LobbyServer/tPoseidon.Kr2014.LobbyServer.csproj",
        "src/Variants/Kr2014/tPoseidon.Kr2014.MasterServer/tPoseidon.Kr2014.MasterServer.csproj",
        "src/Variants/Kr2014/tPoseidon.Kr2014.ChannelServer/tPoseidon.Kr2014.ChannelServer.csproj",
        "src/Variants/Kr2014/tPoseidon.Kr2014.SystemServer/tPoseidon.Kr2014.SystemServer.csproj",
        "src/Variants/Kr2014/tPoseidon.Kr2014.GameServer/tPoseidon.Kr2014.GameServer.csproj",
        "src/tPoseidon.WebShop/tPoseidon.WebShop.csproj",
        "src/tPoseidon.Tools.BuildWorldSnapshot/tPoseidon.Tools.BuildWorldSnapshot.csproj",
    };

    private static readonly string[] Documents =
    {
        ".github/assets/logo.png",
        "README.md",
        "CHANGELOG.md",
        "LICENSE",
        "NOTICE",
        "CONTRIBUTING.md",
        "SECURITY.md",
        "CODE_OF_CONDUCT.md",
        "docs/beta/BETA.md",
        "docs/beta/how-to-report-a-bug.md",
        "docs/beta/forum-bug-report-template.md",
        "docs/beta/SYSTEMS.md",
        "docs/install/postgresql.md",
        "docs/install/hosting-securely.md",
        "docs/install/launcher-updates.md",
        "docs/scripting/README.md",
        "docs/scripting/examples/daily_login_reward.tscript",
        "docs/scripting/examples/fetch_quest.tscript",
        "docs/scripting/examples/gm_event.tscript",
        "docs/scripting/examples/greeter.tscript",
    };

    private static readonly string[] LauncherRids = { "win-x64" };

    private static readonly string[] LauncherDocuments =
    {
        "README.md",
        "LICENSE",
        "NOTICE",
    };

    private const string LauncherProject = "src/tPoseidon.Launcher/tPoseidon.Launcher.csproj";
    private const string InstallerProject = "src/tPoseidon.Launcher.Installer/tPoseidon.Launcher.Installer.csproj";
    private const string LauncherKitFolder = "launcher-kit";
    private const string WindowsPlayerLauncher = "@echo off\r\nstart \"\" \"%~dp0app\\tPoseidon.Launcher.exe\" %*\r\n";
    private const string WindowsLauncher = "@echo off\r\n\"%~dp0app\\tposeidon.exe\" %*\r\n";
    private const string UnixLauncher = "#!/bin/sh\nhere=$(cd \"$(dirname \"$0\")\" && pwd)\nexec \"$here/app/tposeidon\" \"$@\"\n";
    private const string WindowsPanelLauncher = "@echo off\r\nstart \"\" \"%~dp0app\\tPoseidon.Panel.exe\" %*\r\n";
    private const string UnixPanelLauncher = "#!/bin/sh\nhere=$(cd \"$(dirname \"$0\")\" && pwd)\nexec \"$here/app/tPoseidon.Panel\" \"$@\"\n";

    private readonly string _repoRoot;
    private readonly PackOptions _options;
    private readonly ReleaseLock _lock;
    private readonly string _output;
    private readonly string _stageRoot;
    private readonly string _cache;
    private readonly BuildIdentity _identity;

    public PackageBuilder(string repoRoot, PackOptions options, ReleaseLock releaseLock)
    {
        _repoRoot = repoRoot;
        _options = options;
        _lock = releaseLock;
        _output = Path.GetFullPath(options.Output ?? Path.Combine(repoRoot, "artifacts", "release"));
        _stageRoot = Path.Combine(repoRoot, "artifacts", "stage");
        _cache = Path.GetFullPath(options.Cache ?? Path.Combine(repoRoot, "artifacts", "cache"));
        var commit = PackageTools.GitHead(repoRoot);
        _identity = new BuildIdentity(options.Version, commit, BuildIdentity.ComputeTag(options.Version, commit));
    }

    public BuildIdentity Identity => _identity;

    public string Output => _output;

    public async Task<PackageResult> BuildAsync(string rid, CancellationToken ct)
    {
        var name = $"tposeidon-{_options.Version}-{rid}";
        var stage = Path.Combine(_stageRoot, name);
        if (Directory.Exists(stage))
        {
            Directory.Delete(stage, recursive: true);
        }

        var app = Path.Combine(stage, "app");
        Directory.CreateDirectory(app);
        Console.WriteLine($"{rid}: publishing {PublishedProjects.Length} projects");
        foreach (var project in PublishedProjects)
        {
            Publish(project, rid, app);
        }

        CopyMigrations(Path.Combine(_repoRoot, "scripts", "migrations"), Path.Combine(app, "migrations"));
        if (HasLauncher(rid))
        {
            PublishLauncherKit(rid, Path.Combine(app, LauncherKitFolder));
        }

        var licenseFiles = await PackageTools.LicensesAsync(_lock.Licenses, _cache, ct).ConfigureAwait(false);

        Directory.CreateDirectory(_output);
        var isWindows = rid.StartsWith("win-", StringComparison.Ordinal);
        var archivePath = Path.Combine(_output, name + (isWindows ? ".zip" : ".tar.gz"));
        IReadOnlyList<PackageEntry> entries;
        using (var archive = new PackageArchive(archivePath, zip: isWindows, comment: $"{ReleaseManifest.Product} {_identity}"))
        {
            foreach (var file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(stage, file).Replace('\\', '/');
                archive.AddFile(file, name + "/" + relative, PackageTools.ModeFor(file));
            }

            foreach (var document in Documents)
            {
                archive.AddFile(Path.Combine(_repoRoot, document), name + "/" + document, PackageArchive.RegularMode);
            }

            foreach (var (source, entry) in licenseFiles)
            {
                archive.AddFile(source, name + "/" + entry, PackageArchive.RegularMode);
            }

            if (isWindows)
            {
                archive.AddText(name + "/tposeidon.cmd", WindowsLauncher, PackageArchive.RegularMode);
                archive.AddText(name + "/tposeidon-panel.cmd", WindowsPanelLauncher, PackageArchive.RegularMode);
            }
            else
            {
                archive.AddText(name + "/tposeidon", UnixLauncher, PackageArchive.ExecutableMode);
                archive.AddText(name + "/tposeidon-panel", UnixPanelLauncher, PackageArchive.ExecutableMode);
            }

            entries = archive.Entries;
        }

        var archiveInfo = new FileInfo(archivePath);
        return new PackageResult(archivePath, archiveInfo.Length, await PackageTools.HashOfAsync(archivePath, ct).ConfigureAwait(false), entries);
    }

    public static bool HasLauncher(string rid) => LauncherRids.Contains(rid, StringComparer.Ordinal);

    public async Task<PackageResult> BuildLauncherAsync(string rid, CancellationToken ct)
    {
        if (!HasLauncher(rid))
        {
            throw new InvalidOperationException($"There is no player launcher for {rid}.");
        }

        var name = $"{ReleaseManifest.FilePrefix}-launcher-{_options.Version}-{rid}";
        var stage = Path.Combine(_stageRoot, name);
        if (Directory.Exists(stage))
        {
            Directory.Delete(stage, recursive: true);
        }

        var app = Path.Combine(stage, "app");
        Directory.CreateDirectory(app);
        Console.WriteLine($"{rid}: publishing the player launcher");
        Publish(LauncherProject, rid, app);

        var licenseFiles = await PackageTools.LicensesAsync(_lock.Licenses, _cache, ct).ConfigureAwait(false);

        Directory.CreateDirectory(_output);
        var archivePath = Path.Combine(_output, name + ".zip");
        IReadOnlyList<PackageEntry> entries;
        using (var archive = new PackageArchive(archivePath, zip: true, comment: $"{ReleaseManifest.Product} launcher {_identity}"))
        {
            foreach (var file in Directory.EnumerateFiles(stage, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            {
                var relative = Path.GetRelativePath(stage, file).Replace('\\', '/');
                archive.AddFile(file, name + "/" + relative, PackageTools.ModeFor(file));
            }

            foreach (var document in LauncherDocuments)
            {
                archive.AddFile(Path.Combine(_repoRoot, document), name + "/" + document, PackageArchive.RegularMode);
            }

            foreach (var (source, entry) in licenseFiles)
            {
                archive.AddFile(source, name + "/" + entry, PackageArchive.RegularMode);
            }

            archive.AddText(name + "/tposeidon-launcher.cmd", WindowsPlayerLauncher, PackageArchive.RegularMode);
            entries = archive.Entries;
        }

        var archiveInfo = new FileInfo(archivePath);
        return new PackageResult(archivePath, archiveInfo.Length, await PackageTools.HashOfAsync(archivePath, ct).ConfigureAwait(false), entries);
    }

    private void PublishLauncherKit(string rid, string kit)
    {
        Console.WriteLine($"{rid}: publishing the launcher kit for the panel");
        PackageTools.Publish(_repoRoot, LauncherProject, rid, Path.Combine(kit, "launcher"), _identity, ["-p:ApplicationIcon="]);
        PackageTools.Publish(
            _repoRoot,
            InstallerProject,
            rid,
            Path.Combine(kit, "installer"),
            _identity,
            ["-p:PublishSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:EnableCompressionInSingleFile=true"]);
    }

    private void Publish(string project, string rid, string output) =>
        PackageTools.Publish(_repoRoot, project, rid, output, _identity, []);

    private static void CopyMigrations(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source).Where(path => MigrationFiles.IsNumbered(Path.GetFileName(path))))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }
    }
}
