using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting;

public static class ReferencedTablesClientData
{
    public const string Id = ReferencedTables.Id;
    public const string Title = ReferencedTables.Title;

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
        var missing = LibConfigXmlTableAdder.MissingTables(xml, ReferencedTables.Tables);
        return missing.Count == 0
            ? new ClientDataCheck(false, "The tables that the client data names are already in the client data.")
            : new ClientDataCheck(true, $"The client data names {missing.Count} tables it does not contain (pet speech, NPC messages, quest rewards, map sounds, shops and event monster lists), so the client shows nothing for them.");
    }

    public static ClientDataOutcome Apply(string libconfigPath, string? executablePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libconfigPath);
        var xml = LibConfigDatDecryptor.DecryptFile(libconfigPath);
        if (!Check(xml).Needed)
        {
            return new ClientDataOutcome(false, "The tables that the client data names are already in the client data.");
        }

        ShadowSanctuaryClientData.RequireClientClosed(executablePath);
        var edit = LibConfigXmlTableAdder.AddMissingTables(xml, ReferencedTables.Tables);
        var encoded = LibConfigDatEncryptor.Encode(edit.Xml);
        var idxPath = ShadowSanctuaryClientData.IndexPath(libconfigPath);
        try
        {
            ShadowSanctuaryClientData.Backup(libconfigPath);
            ShadowSanctuaryClientData.Backup(idxPath);
            ShadowSanctuaryClientData.Replace(libconfigPath, encoded.Dat);
            ShadowSanctuaryClientData.Replace(idxPath, encoded.Idx);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not update {libconfigPath}: {ex.Message}", "Move the client to a folder you can write to (not Program Files), or give your account write access to it.", ex);
        }
        catch (IOException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientRunning, $"Could not update {libconfigPath}: {ex.Message}", "Close the game client, then try again.", ex);
        }

        return new ClientDataOutcome(true, $"Added {edit.Added} tables to {Path.GetFileName(libconfigPath)}; the original is kept as {Path.GetFileName(libconfigPath)}{ClientAddressPatch.BackupSuffix}.");
    }
}
