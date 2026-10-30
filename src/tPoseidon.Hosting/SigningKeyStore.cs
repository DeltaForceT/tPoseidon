using System.Security.Cryptography;
using System.Text.Json;
using tPoseidon.Core.Common.Launcher;

namespace tPoseidon.Hosting;

public sealed record SigningKey(string PrivateKey, string PublicKey, IReadOnlyList<KeyLink> Rotations)
{
    public const int FingerprintLength = 16;

    public SigningKeyPair Pair => new(PrivateKey, PublicKey);

    public string Fingerprint => Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(PublicKey)))[..FingerprintLength].ToLowerInvariant();
}

public static class SigningKeyStore
{
    public const string BackupSuffix = ".launcher-signing-key.json";

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static bool Exists(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return File.Exists(paths.SigningKey);
    }

    public static SigningKey? Load(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (!File.Exists(paths.SigningKey))
        {
            return null;
        }

        try
        {
            var key = JsonSerializer.Deserialize<SigningKey>(File.ReadAllText(paths.SigningKey), Options);
            if (key is null || key.Rotations is null || !ManifestSignature.IsKeyPair(key.Pair) || key.Rotations.Count > ManifestSignature.MaxLinks)
            {
                throw Damaged(paths, null);
            }

            return key;
        }
        catch (JsonException ex)
        {
            throw Damaged(paths, ex);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The launcher signing key {paths.SigningKey} could not be read: {ex.Message}", ex);
        }
    }

    public static SigningKey EnsureCreated(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (Load(paths) is { } existing)
        {
            return existing;
        }

        var pair = ManifestSignature.CreateKeyPair();
        var created = new SigningKey(pair.PrivateKey, pair.PublicKey, []);
        Save(paths, created);
        return created;
    }

    public static SigningKey Rotate(InstallPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        var current = Load(paths)
            ?? throw new HostingException("There is no launcher signing key to change yet.", "Publish once or build a launcher first; that creates the key.", null);
        if (current.Rotations.Count >= ManifestSignature.MaxLinks)
        {
            throw new HostingException($"The key has been changed {ManifestSignature.MaxLinks} times already, which is the most launchers can follow.", "Build and hand out new launchers, then use the new key from them.", null);
        }

        var next = ManifestSignature.CreateKeyPair();
        var link = ManifestSignature.Endorse(current.Pair, next.PublicKey);
        var rotated = new SigningKey(next.PrivateKey, next.PublicKey, [.. current.Rotations, link]);
        Save(paths, rotated);
        return rotated;
    }

    public static string? BackupTo(InstallPaths paths, string databaseBackup)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseBackup);
        if (!File.Exists(paths.SigningKey))
        {
            return null;
        }

        var target = Path.ChangeExtension(databaseBackup, null) + BackupSuffix;
        try
        {
            File.Copy(paths.SigningKey, target, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The launcher signing key could not be copied to {target}: {ex.Message}", ex);
        }

        return target;
    }

    private static void Save(InstallPaths paths, SigningKey key)
    {
        try
        {
            Directory.CreateDirectory(paths.Root);
            var temporary = paths.SigningKey + ".tmp";
            File.WriteAllBytes(temporary, JsonSerializer.SerializeToUtf8Bytes(key, Options));
            File.Move(temporary, paths.SigningKey, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The launcher signing key could not be saved to {paths.SigningKey}: {ex.Message}", ex);
        }
    }

    private static HostingException Damaged(InstallPaths paths, Exception? inner) =>
        new($"The launcher signing key {paths.SigningKey} is damaged.", "Restore it from your backup. Replacing it makes every launcher you handed out refuse your updates.", inner);
}
