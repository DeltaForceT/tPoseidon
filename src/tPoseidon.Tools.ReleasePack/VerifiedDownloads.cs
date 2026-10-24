using System.Security.Cryptography;

namespace tPoseidon.Tools.ReleasePack;

internal sealed class VerifiedDownloads : IDisposable
{
    private readonly string _cacheDirectory;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(10) };

    public VerifiedDownloads(string cacheDirectory)
    {
        _cacheDirectory = cacheDirectory;
        Directory.CreateDirectory(cacheDirectory);
    }

    public async Task<string> FetchAsync(Uri url, string sha256, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sha256))
        {
            throw new InvalidDataException($"No SHA-256 pinned for {url}.");
        }

        var target = Path.Combine(_cacheDirectory, sha256.ToLowerInvariant() + "-" + Path.GetFileName(url.AbsolutePath));
        if (File.Exists(target) && string.Equals(await HashOfAsync(target, ct).ConfigureAwait(false), sha256, StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        Console.WriteLine($"  downloading {url}");
        var partial = target + ".part";
        await using (var response = await _http.GetStreamAsync(url, ct).ConfigureAwait(false))
        await using (var file = File.Create(partial))
        {
            await response.CopyToAsync(file, ct).ConfigureAwait(false);
        }

        var actual = await HashOfAsync(partial, ct).ConfigureAwait(false);
        if (!string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(partial);
            throw new InvalidDataException($"SHA-256 mismatch for {url}: expected {sha256}, got {actual}.");
        }

        File.Move(partial, target, overwrite: true);
        return target;
    }

    public void Dispose() => _http.Dispose();

    private static async Task<string> HashOfAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
