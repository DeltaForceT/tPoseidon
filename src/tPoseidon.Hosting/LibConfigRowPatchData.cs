using tPoseidon.Core.LibConfig;

namespace tPoseidon.Hosting;

public sealed class LibConfigRowPatchData
{
    private readonly IReadOnlyList<LibConfigRowPatch> _patches;
    private readonly string _subject;

    public LibConfigRowPatchData(IReadOnlyList<LibConfigRowPatch> patches, string subject)
    {
        ArgumentNullException.ThrowIfNull(patches);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        _patches = patches;
        _subject = subject;
    }

    public ClientDataCheck Check(string libconfigPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libconfigPath);
        if (!File.Exists(libconfigPath))
        {
            return new ClientDataCheck(false, $"{Path.GetFileName(libconfigPath)} does not exist.");
        }

        return Check(LibConfigDatDecryptor.DecryptFile(libconfigPath));
    }

    public ClientDataCheck Check(byte[] xml)
    {
        var missing = _patches
            .Select(patch => (patch.Table, Keys: LibConfigXmlRowEditor.MissingKeys(xml, patch)))
            .Where(entry => entry.Keys.Count > 0)
            .ToList();
        return missing.Count == 0
            ? new ClientDataCheck(false, $"The {_subject} are already in the client data.")
            : new ClientDataCheck(true, "The client data has no " + string.Join(" and ", missing.Select(entry => $"{entry.Table} rows for maps {string.Join(", ", entry.Keys)}")) + $", so the {_subject} cannot be entered.");
    }

    public ClientDataOutcome Apply(string libconfigPath, string? executablePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libconfigPath);
        var xml = LibConfigDatDecryptor.DecryptFile(libconfigPath);
        if (!Check(xml).Needed)
        {
            return new ClientDataOutcome(false, $"The {_subject} are already in the client data.");
        }

        ShadowSanctuaryClientData.RequireClientClosed(executablePath);
        var added = 0;
        foreach (var patch in _patches)
        {
            var edit = LibConfigXmlRowEditor.AddMissingRows(xml, patch);
            xml = edit.Xml;
            added += edit.Added;
        }

        var encoded = LibConfigDatEncryptor.Encode(xml);
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

        return new ClientDataOutcome(true, $"Added {added} map rows to {Path.GetFileName(libconfigPath)}; the original is kept as {Path.GetFileName(libconfigPath)}{ClientAddressPatch.BackupSuffix}.");
    }
}
