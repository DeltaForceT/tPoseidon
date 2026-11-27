using System.Formats.Tar;
using System.IO.Compression;
using System.Net.Http.Headers;
using tPoseidon.Core.Common.Updates;

namespace tPoseidon.Hosting.Updates;

public sealed record StagedUpdate(string Version, string Folder);

public sealed class UpdateStager
{
    public const int MaxListBytes = 1024 * 1024;
    public const long MaxPackageBytes = 4L * 1024 * 1024 * 1024;

    private readonly HttpClient _http;
    private readonly UpdateWork _work;
    private readonly string _publicKey;

    public UpdateStager(HttpClient http, UpdateWork work, string publicKey)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _work = work ?? throw new ArgumentNullException(nameof(work));
        _publicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
    }

    public async Task<StagedUpdate> StageAsync(UpdateOffer offer, IProgress<double>? progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(offer);
        var checksums = await FetchSmallAsync(offer.Checksums.Url, ct).ConfigureAwait(false);
        var signature = await FetchSmallAsync(offer.Signature.Url, ct).ConfigureAwait(false);
        var entries = ReleaseVerifier.VerifyChecksumList(checksums, signature, _publicKey);

        UpdateWork.DeleteFolder(_work.Download);
        Directory.CreateDirectory(_work.Download);
        var packagePath = Path.Combine(_work.Download, Path.GetFileName(offer.Package.Name));
        await DownloadAsync(offer.Package.Url, packagePath, offer.Package.Size, progress, ct).ConfigureAwait(false);
        ReleaseVerifier.VerifyPackage(packagePath, offer.Package.Name, entries);

        UpdateWork.DeleteFolder(_work.Staging);
        Directory.CreateDirectory(_work.Staging);
        try
        {
            Extract(packagePath, _work.Staging);
            RequireProgram(_work.Staging);
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            UpdateWork.DeleteFolder(_work.Staging);
            throw new HostingException($"The update was not installed: the package could not be unpacked ({ex.Message}).", "Nothing was changed.", ex);
        }

        File.Delete(packagePath);
        return new StagedUpdate(offer.Version.ToString(), _work.Staging);
    }

    public static void Extract(string archive, string target)
    {
        var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        string? top = null;

        string? Destination(string entryName)
        {
            var parts = entryName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return null;
            }

            if (parts.Any(part => part is ".." or "." || part.Contains(':', StringComparison.Ordinal)) || Path.IsPathRooted(entryName))
            {
                throw new InvalidDataException($"the package has an unsafe path ({entryName}).");
            }

            top ??= parts[0];
            if (!string.Equals(top, parts[0], StringComparison.Ordinal))
            {
                throw new InvalidDataException("the package has more than one top folder.");
            }

            if (parts.Length == 1)
            {
                return null;
            }

            var full = Path.GetFullPath(Path.Combine(target, string.Join(Path.DirectorySeparatorChar, parts.Skip(1))));
            return full.StartsWith(root, StringComparison.Ordinal) ? full : throw new InvalidDataException($"the package has an unsafe path ({entryName}).");
        }

        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            using var zip = ZipFile.OpenRead(archive);
            foreach (var entry in zip.Entries)
            {
                var destination = Destination(entry.FullName);
                if (destination is null)
                {
                    continue;
                }

                if (entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\'))
                {
                    Directory.CreateDirectory(destination);
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
                var mode = (UnixFileMode)((entry.ExternalAttributes >> 16) & 0xFFF);
                if (!OperatingSystem.IsWindows() && mode != 0)
                {
                    File.SetUnixFileMode(destination, mode);
                }
            }

            return;
        }

        using var file = File.OpenRead(archive);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var tar = new TarReader(gzip);
        while (tar.GetNextEntry() is { } entry)
        {
            if (entry.EntryType is TarEntryType.GlobalExtendedAttributes or TarEntryType.ExtendedAttributes)
            {
                continue;
            }

            var destination = Destination(entry.Name);
            if (destination is null)
            {
                continue;
            }

            if (entry.EntryType == TarEntryType.Directory)
            {
                Directory.CreateDirectory(destination);
                continue;
            }

            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
            {
                throw new InvalidDataException($"the package has an entry that is not a plain file ({entry.Name}).");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: true);
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(destination, entry.Mode);
            }
        }
    }

    public static void RequireProgram(string staging)
    {
        var program = Path.Combine(staging, "app", InstallPaths.Executable("tposeidon"));
        if (!File.Exists(program))
        {
            throw new InvalidDataException("it has no app/tposeidon program.");
        }
    }

    private async Task<byte[]> FetchSmallAsync(Uri url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(GitHubReleaseFeed.UserAgent, "1"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > MaxListBytes)
            {
                throw new HostingException($"The update was not installed: {url.Segments[^1]} is too large.", "Nothing was changed.", null);
            }

            var content = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            return content.Length > MaxListBytes
                ? throw new HostingException($"The update was not installed: {url.Segments[^1]} is too large.", "Nothing was changed.", null)
                : content;
        }
        catch (HttpRequestException ex)
        {
            throw new HostingException($"The update could not be downloaded: {ex.Message}", "Nothing was changed.", ex);
        }
    }

    private async Task DownloadAsync(Uri url, string target, long expectedSize, IProgress<double>? progress, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue(GitHubReleaseFeed.UserAgent, "1"));
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? expectedSize;
            if (total > MaxPackageBytes)
            {
                throw new HostingException("The update was not installed: the package is far larger than expected.", "Nothing was changed.", null);
            }

            await using var source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var destination = File.Create(target);
            var buffer = new byte[81920];
            long done = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                done += read;
                if (done > MaxPackageBytes)
                {
                    throw new HostingException("The update was not installed: the package is far larger than expected.", "Nothing was changed.", null);
                }

                if (total > 0)
                {
                    progress?.Report((double)done / total);
                }
            }
        }
        catch (HttpRequestException ex)
        {
            throw new HostingException($"The update could not be downloaded: {ex.Message}", "Nothing was changed.", ex);
        }
    }
}
