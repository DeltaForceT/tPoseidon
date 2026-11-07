namespace tPoseidon.Core.Crypto.Trident;

public static class KeyTableFile
{
    public const string FileName = "trident-keytable.bin";
    public const string LegacyFileName = "keytable-s2v2010.bin";

    public static bool MigrateLegacy(string keyTablePath, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyTablePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(keyTablePath));
        if (directory is null || !string.Equals(Path.GetFileName(keyTablePath), FileName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var legacyPath = Path.Combine(directory, LegacyFileName);
        if (File.Exists(keyTablePath) || !File.Exists(legacyPath))
        {
            return false;
        }

        File.Move(legacyPath, keyTablePath);
        log?.Invoke($"key table renamed to {FileName}");
        return true;
    }
}
