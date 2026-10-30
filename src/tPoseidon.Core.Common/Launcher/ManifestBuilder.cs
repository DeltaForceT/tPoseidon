using System.Security.Cryptography;

namespace tPoseidon.Core.Common.Launcher;

public static class ManifestBuilder
{
    public const string ClientFolder = "client";
    public const string PacksFolder = "packs";
    public const string NewsFolder = "news";
    public const string ChannelFile = "channel.json";
    public const string ManifestFile = "manifest.json";
    public const string LauncherFolder = "launcher";

    public static readonly IReadOnlyList<string> BannerNames = ["banner.png", "banner.jpg", "banner.jpeg"];

    public static LauncherManifest Build(string updatesFolder, string defaultName, string? launcherVersion)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(updatesFolder);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultName);
        var channel = ChannelSettings.Read(updatesFolder);
        var banner = FindBanner(updatesFolder);
        var manifest = new LauncherManifest(
            channel.Name ?? defaultName,
            channel.Color,
            banner,
            string.IsNullOrWhiteSpace(channel.Maintenance) ? null : channel.Maintenance,
            channel.LauncherVersion ?? launcherVersion,
            new NewsStore(Path.Combine(updatesFolder, NewsFolder)).Load().Take(LauncherManifest.MaxNews).Select(post => post.ToItem()).ToList(),
            ReadFiles(updatesFolder),
            banner is null ? null : Hash(Path.Combine(updatesFolder, banner)),
            ReadLauncher(updatesFolder));
        manifest.Validate();
        return manifest;
    }

    public static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string? FindBanner(string updatesFolder) =>
        BannerNames.FirstOrDefault(name => File.Exists(Path.Combine(updatesFolder, name)) && new FileInfo(Path.Combine(updatesFolder, name)).Length <= LauncherManifest.MaxBannerBytes);

    private static LauncherRelease? ReadLauncher(string updatesFolder)
    {
        var package = LauncherPackageInfo.Read(updatesFolder);
        var folder = Path.Combine(updatesFolder, LauncherFolder);
        if (package is null || !Directory.Exists(folder))
        {
            return null;
        }

        var files = new List<ManifestFile>();
        AddFiles(files, folder, null);
        return new LauncherRelease(package.Version, package.Executable, files);
    }

    private static List<ManifestFile> ReadFiles(string updatesFolder)
    {
        var files = new List<ManifestFile>();
        AddFiles(files, Path.Combine(updatesFolder, ClientFolder), null);
        var packs = Path.Combine(updatesFolder, PacksFolder);
        if (Directory.Exists(packs))
        {
            foreach (var pack in Directory.EnumerateDirectories(packs).Order(StringComparer.Ordinal))
            {
                AddFiles(files, pack, Path.GetFileName(pack));
            }
        }

        return files;
    }

    private static void AddFiles(List<ManifestFile> files, string folder, string? group)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(folder, path).Replace(Path.DirectorySeparatorChar, '/');
            files.Add(new ManifestFile(relative, new FileInfo(path).Length, Hash(path), group));
        }
    }
}
