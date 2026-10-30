namespace tPoseidon.Hosting.ClientScan;

public sealed class LibConfigFilesFix : IClientFix
{
    public const string FixId = "libconfig-files";

    public string Id => FixId;

    public string Title => "Make the missing libconfig, index and font files";

    public bool CanRestore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ClientFixLog.Load(context.ClientFolder).CreatedFiles.Count > 0;
    }

    public FixOutcome Apply(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var copies = context.Plan.Copies;
        if (copies.Count == 0)
        {
            return new FixOutcome(false, "The libconfig, index and font files are already there.");
        }

        var log = ClientFixLog.Load(context.ClientFolder);
        foreach (var copy in copies)
        {
            ClientFolder.CopyFile(copy);
            log.CreatedFiles.Add(Path.GetRelativePath(context.ClientFolder, copy.Target));
        }

        log.Save(context.ClientFolder);
        return new FixOutcome(true, $"Made {string.Join(", ", copies.Select(copy => Path.GetFileName(copy.Target)))}.");
    }

    public FixOutcome Restore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var log = ClientFixLog.Load(context.ClientFolder);
        if (log.CreatedFiles.Count == 0)
        {
            return new FixOutcome(false, "The scanner has not made any files in this client.");
        }

        var removed = new List<string>();
        foreach (var relative in log.CreatedFiles)
        {
            var path = Path.Combine(context.ClientFolder, relative);
            if (File.Exists(path))
            {
                File.Delete(path);
                removed.Add(Path.GetFileName(path));
            }
        }

        log.CreatedFiles.Clear();
        log.Save(context.ClientFolder);
        return new FixOutcome(removed.Count > 0, removed.Count > 0 ? $"Removed {string.Join(", ", removed)}." : "The files the scanner made are already gone.");
    }
}
