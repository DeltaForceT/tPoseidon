using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting;

public sealed record ClientDataCheck(bool Needed, string Reason);

public sealed record ClientDataOutcome(bool Changed, string Message);

public static class ShadowSanctuaryClientData
{
    public const string Id = "shadow-sanctuary-rows";
    public const string Title = "Add the Shadow Sanctuary map rows to the client data";

    public static ClientDataCheck Check(string libconfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libconfigPath);
        if (!File.Exists(libconfigPath))
        {
            return new ClientDataCheck(false, $"{Path.GetFileName(libconfigPath)} does not exist.");
        }

        return Check(LibConfigDatDecryptor.DecryptFile(libconfigPath));
    }

    public static ClientDataCheck Check(byte[] xml)
    {
        var missing = ShadowSanctuaryRows.Patches
            .Select(patch => (patch.Table, Keys: LibConfigXmlRowEditor.MissingKeys(xml, patch)))
            .Where(entry => entry.Keys.Count > 0)
            .ToList();
        var unlinked = ShadowSanctuaryRows.TimerNpcs.Where(patch => LibConfigXmlRowEditor.FieldsPending(xml, patch)).ToList();
        if (missing.Count == 0 && unlinked.Count == 0)
        {
            return new ClientDataCheck(false, "The Shadow Sanctuary and Harcon Sanctuary rows are already in the client data.");
        }

        var parts = missing.Select(entry => $"{entry.Table} rows for {(entry.Table is "MapInfoEx" or "MonsterScenario" ? "maps" : "ids")} {string.Join(", ", entry.Keys)}").ToList();
        if (unlinked.Count > 0)
        {
            parts.Add($"timer links on the Shadow Sanctuary teleport NPC in {unlinked.Count} town tables");
        }

        return new ClientDataCheck(true, "The client data has no " + string.Join(" and no ", parts) + ", so the client cannot enter those maps or see the timed teleport.");
    }

    public static ClientDataOutcome Apply(string libconfigPath, string? executablePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libconfigPath);
        var xml = LibConfigDatDecryptor.DecryptFile(libconfigPath);
        if (!Check(xml).Needed)
        {
            return new ClientDataOutcome(false, "The Shadow Sanctuary map rows are already in the client data.");
        }

        RequireClientClosed(executablePath);
        var added = ShadowSanctuaryRows.Patches.Sum(patch => LibConfigXmlRowEditor.MissingKeys(xml, patch).Count);
        xml = ShadowSanctuaryRows.Merge(xml);
        var encoded = LibConfigDatEncryptor.Encode(xml);
        var idxPath = IndexPath(libconfigPath);
        try
        {
            Backup(libconfigPath);
            Backup(idxPath);
            Replace(libconfigPath, encoded.Dat);
            Replace(idxPath, encoded.Idx);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not update {libconfigPath}: {ex.Message}", "Move the client to a folder you can write to (not Program Files), or give your account write access to it.", ex);
        }
        catch (IOException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientRunning, $"Could not update {libconfigPath}: {ex.Message}", "Close the game client, then try again.", ex);
        }

        return new ClientDataOutcome(true, $"Added {added} rows and linked the timer NPCs in {Path.GetFileName(libconfigPath)}; the original is kept as {Path.GetFileName(libconfigPath)}{ClientAddressPatch.BackupSuffix}.");
    }

    public static bool CanRestore(string libconfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libconfigPath);
        return File.Exists(libconfigPath + ClientAddressPatch.BackupSuffix);
    }

    public static ClientDataOutcome Restore(string libconfigPath, string? executablePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libconfigPath);
        if (!CanRestore(libconfigPath))
        {
            return new ClientDataOutcome(false, $"There is no saved original of {Path.GetFileName(libconfigPath)}.");
        }

        RequireClientClosed(executablePath);
        var idxPath = IndexPath(libconfigPath);
        try
        {
            RestoreFile(libconfigPath);
            if (File.Exists(idxPath + ClientAddressPatch.BackupSuffix))
            {
                RestoreFile(idxPath);
            }
            else if (File.Exists(idxPath))
            {
                File.Delete(idxPath);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not restore {libconfigPath}: {ex.Message}", "Give your account write access to the client folder.", ex);
        }
        catch (IOException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientRunning, $"Could not restore {libconfigPath}: {ex.Message}", "Close the game client, then try again.", ex);
        }

        return new ClientDataOutcome(true, $"Restored {Path.GetFileName(libconfigPath)} to the original.");
    }

    internal static string IndexPath(string libconfigPath) => Path.ChangeExtension(libconfigPath, ".idx");

    internal static void RequireClientClosed(string? executablePath)
    {
        if (executablePath is not null && RunningClient.IsRunning(executablePath))
        {
            throw new PlayFailure(PlayFailureReason.ClientRunning, $"{Path.GetFileName(executablePath)} is running from this folder.", "Close the game client, then try again.", null);
        }
    }

    internal static void Backup(string path)
    {
        var backup = path + ClientAddressPatch.BackupSuffix;
        if (File.Exists(path) && !File.Exists(backup))
        {
            File.Copy(path, backup);
        }
    }

    internal static void Replace(string path, byte[] bytes)
    {
        var temporary = path + ".tposeidon-new";
        File.WriteAllBytes(temporary, bytes);
        File.Move(temporary, path, overwrite: true);
    }

    private static void RestoreFile(string path)
    {
        File.Move(path + ClientAddressPatch.BackupSuffix, path, overwrite: true);
    }
}
