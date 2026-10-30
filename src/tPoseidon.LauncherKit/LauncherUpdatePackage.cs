using System.IO.Compression;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public sealed record LauncherImport(LauncherPackageInfo Info, int Files, long Bytes);

public static class LauncherUpdatePackage
{
    public const string TestLauncherWarning = "This is a test launcher: it only works on the computer that runs the server. Players who receive it as an update could not connect.";

    private const string BrandingEntry = BrandingSpec.FolderName + "/" + BrandingSpec.FileName;
    private const string StagingSuffix = ".new";

    public static LauncherImport Import(InstallPaths paths, string zipPath)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        var staging = Path.Combine(paths.Updates, ManifestBuilder.LauncherFolder + StagingSuffix);
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var root = RootOf(zip);
            var spec = ReadSpec(zip, root);
            var key = SigningKeyStore.Load(paths);
            if (spec.PublicKey is null || key is null || (spec.PublicKey != key.PublicKey && key.Rotations.All(link => link.From != spec.PublicKey)))
            {
                throw new HostingException(
                    "This launcher was not built with this server's signing key, so its players could not trust the update.",
                    "Build the launcher again with the control panel on this server.",
                    null);
            }

            var executable = LauncherBuilder.ExecutableName(spec.ServerName);
            var entries = zip.Entries
                .Where(entry => entry.FullName.StartsWith(root, StringComparison.Ordinal) && entry.Name.Length > 0)
                .Select(entry => (Entry: entry, Relative: entry.FullName[root.Length..]))
                .Where(each => each.Relative != LauncherBuilder.InstallNotes)
                .ToList();
            if (entries.Count == 0 || entries.Count > LauncherManifest.MaxLauncherFiles || entries.Sum(each => each.Entry.Length) > LauncherManifest.MaxLauncherBytes)
            {
                throw new HostingException("The launcher zip has no files, or too many or too big ones.");
            }

            if (!entries.Any(each => each.Relative == executable))
            {
                throw new HostingException($"The launcher zip does not hold {executable}.", "Use the zip the control panel built for this server.", null);
            }

            Reset(staging);
            foreach (var (entry, relative) in entries)
            {
                var target = ClientPath.Resolve(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: false);
            }

            var live = Path.Combine(paths.Updates, ManifestBuilder.LauncherFolder);
            Reset(live);
            Directory.Move(staging, live);
            var info = new LauncherPackageInfo(spec.LauncherVersion!, executable, spec.TestOnThisComputer);
            info.Write(paths.Updates);
            return new LauncherImport(info, entries.Count, entries.Sum(each => each.Entry.Length));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Reset(staging);
            throw new HostingException($"The launcher zip {Path.GetFileName(zipPath)} could not be used: {ex.Message}", ex);
        }
    }

    public static void Remove(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        try
        {
            LauncherPackageInfo.Remove(paths.Updates);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The launcher update could not be removed: {ex.Message}", ex);
        }
    }

    private static string RootOf(ZipArchive zip)
    {
        var branding = zip.Entries.FirstOrDefault(entry => entry.FullName == BrandingEntry || entry.FullName.EndsWith("/" + BrandingEntry, StringComparison.Ordinal))
            ?? throw new HostingException("This zip is not a launcher built by the control panel.", "It has no branding folder.", null);
        var root = branding.FullName[..^BrandingEntry.Length];
        return root.Count(c => c == '/') > 1 ? throw new HostingException("This zip is not a launcher built by the control panel.") : root;
    }

    private static BrandingSpec ReadSpec(ZipArchive zip, string root)
    {
        using var reader = new StreamReader(zip.GetEntry(root + BrandingEntry)!.Open());
        var spec = BrandingSpec.Parse(reader.ReadToEnd());
        return spec.LauncherVersion is null
            ? throw new HostingException("This launcher was built before launchers could update themselves.", "Build it again with the control panel.", null)
            : spec;
    }

    private static void Reset(string folder)
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
