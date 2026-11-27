using System.Text.Json;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Core.Common.Updates;

namespace tPoseidon.Tools.ReleasePack;

internal static class ReleaseSigning
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string MakeKey(string folder, string repoRoot)
    {
        var target = Path.GetFullPath(folder);
        if (IsInside(target, repoRoot))
        {
            throw new InvalidDataException($"The release key must not be stored inside the repository ({repoRoot}). Choose a folder outside it.");
        }

        var file = Path.Combine(target, ReleaseAssets.KeyFileName);
        if (File.Exists(file))
        {
            throw new InvalidDataException($"{file} already exists. It was not replaced; replacing the release key makes every installed panel refuse your updates.");
        }

        Directory.CreateDirectory(target);
        var pair = ManifestSignature.CreateKeyPair();
        File.WriteAllText(file, JsonSerializer.Serialize(pair, JsonOptions));
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return pair.PublicKey;
    }

    public static SigningKeyPair ReadKey(string file)
    {
        if (!File.Exists(file))
        {
            throw new InvalidDataException($"The release key file {file} does not exist.");
        }

        var pair = JsonSerializer.Deserialize<SigningKeyPair>(File.ReadAllText(file), JsonOptions)
            ?? throw new InvalidDataException($"{file} is empty.");
        if (pair.PrivateKey is null || pair.PublicKey is null || !ManifestSignature.IsKeyPair(pair))
        {
            throw new InvalidDataException($"{file} is not a release key pair.");
        }

        return pair;
    }

    public static IReadOnlyList<string> Sign(IReadOnlyList<string> files, string outputFolder, string keyFile)
    {
        ArgumentNullException.ThrowIfNull(files);
        var key = ReadKey(keyFile);
        var entries = files.Select(path => new ChecksumEntry(ReleaseChecksums.HashOf(path), Path.GetFileName(path))).ToList();
        var sums = ReleaseChecksums.Build(entries);
        var signature = ManifestSignature.Sign(sums, key, []);
        if (ManifestSignature.Verify(sums, signature, key.PublicKey) != SignatureVerdict.Valid)
        {
            throw new InvalidDataException("The signature made with this key does not verify. Nothing was written.");
        }

        Directory.CreateDirectory(outputFolder);
        var sumsPath = Path.Combine(outputFolder, ReleaseAssets.ChecksumFile);
        var signaturePath = Path.Combine(outputFolder, ReleaseAssets.SignatureFile);
        File.WriteAllBytes(sumsPath, sums);
        File.WriteAllBytes(signaturePath, signature);
        return [sumsPath, signaturePath];
    }

    public static IReadOnlyList<string> SignableFilesIn(string folder) =>
        Directory.EnumerateFiles(folder)
            .Where(path => Path.GetFileName(path) is { } name
                && name.StartsWith("tposeidon-", StringComparison.Ordinal)
                && (name.EndsWith(".zip", StringComparison.Ordinal) || name.EndsWith(".tar.gz", StringComparison.Ordinal) || name.EndsWith("-manifest.json", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static bool IsInside(string path, string root)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), path);
        return relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}
