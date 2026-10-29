using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.Hosting;

public sealed record AddOutcome(int Copied, IReadOnlyList<string> Refused);

public static class UpdateFolder
{
    public static AddOutcome AddClientFiles(InstallPaths paths, IReadOnlyList<string> sources, string? targetFolder)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(sources);
        var target = (targetFolder ?? "").Trim().Replace('\\', '/').Trim('/');
        if (target.Length > 0 && !ClientPath.TryNormalize(target, out _))
        {
            throw new HostingException($"'{targetFolder}' is not a folder inside the client.", "Use a path like data/item.", null);
        }

        var root = Path.Combine(paths.Updates, ManifestBuilder.ClientFolder);
        var copied = 0;
        var refused = new List<string>();
        try
        {
            Directory.CreateDirectory(root);
            foreach (var source in sources)
            {
                foreach (var (file, relative) in Expand(source, target, refused))
                {
                    if (Copy(root, file, relative, refused))
                    {
                        copied++;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not add the files to {root}: {ex.Message}", ex);
        }

        return new AddOutcome(copied, refused);
    }

    public static void RemoveFile(InstallPaths paths, string? group, string relative)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var root = group is null
            ? Path.Combine(paths.Updates, ManifestBuilder.ClientFolder)
            : Path.Combine(paths.Updates, ManifestBuilder.PacksFolder, group);
        try
        {
            var full = ClientPath.Resolve(root, relative);
            File.Delete(full);
            Prune(Path.GetDirectoryName(full), Path.GetFullPath(root));
        }
        catch (InvalidDataException ex)
        {
            throw new HostingException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not remove {relative}: {ex.Message}", ex);
        }
    }

    public static PictureInfo SetBanner(InstallPaths paths, byte[] picture)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(picture);
        PictureInfo info;
        try
        {
            info = PictureInfo.Inspect(picture, LauncherManifest.MaxBannerBytes);
        }
        catch (InvalidDataException ex)
        {
            throw new HostingException(ex.Message, "Choose a PNG or JPG picture of at most 2 MB.", ex);
        }

        try
        {
            Directory.CreateDirectory(paths.Updates);
            DeleteBanners(paths);
            File.WriteAllBytes(Path.Combine(paths.Updates, "banner" + info.Extension), picture);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not save the banner: {ex.Message}", ex);
        }

        return info;
    }

    public static void RemoveBanner(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        try
        {
            DeleteBanners(paths);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"Could not remove the banner: {ex.Message}", ex);
        }
    }

    public static string? BannerPath(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return ManifestBuilder.BannerNames.Select(name => Path.Combine(paths.Updates, name)).FirstOrDefault(File.Exists);
    }

    private static void DeleteBanners(InstallPaths paths)
    {
        foreach (var name in ManifestBuilder.BannerNames)
        {
            File.Delete(Path.Combine(paths.Updates, name));
        }
    }

    private static IEnumerable<(string File, string Relative)> Expand(string source, string target, List<string> refused)
    {
        if (File.Exists(source))
        {
            yield return (source, Join(target, Path.GetFileName(source)));
            yield break;
        }

        if (!Directory.Exists(source))
        {
            refused.Add($"{source}: not found");
            yield break;
        }

        var folder = Path.GetFullPath(source);
        var name = Path.GetFileName(folder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint };
        foreach (var file in Directory.EnumerateFiles(folder, "*", options))
        {
            yield return (file, Join(target, name + "/" + Path.GetRelativePath(folder, file).Replace(Path.DirectorySeparatorChar, '/')));
        }
    }

    private static string Join(string target, string relative) => target.Length == 0 ? relative : target + "/" + relative;

    private static bool Copy(string root, string file, string relative, List<string> refused)
    {
        if (!ClientPath.TryNormalize(relative, out var normalized))
        {
            refused.Add($"{relative}: not allowed in the client folder (original game files and reserved names are refused)");
            return false;
        }

        if (new FileInfo(file).Length > LauncherManifest.MaxFileBytes)
        {
            refused.Add($"{relative}: larger than {LauncherManifest.MaxFileBytes >> 20} MB");
            return false;
        }

        var destination = ClientPath.Resolve(root, normalized);
        if (string.Equals(Path.GetFullPath(file), destination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(file, destination, overwrite: true);
        return true;
    }

    private static void Prune(string? folder, string root)
    {
        while (folder is not null && !string.Equals(Path.GetFullPath(folder), root, StringComparison.OrdinalIgnoreCase) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
            folder = Path.GetDirectoryName(folder);
        }
    }
}
