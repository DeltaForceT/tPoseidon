namespace tPoseidon.Hosting.ClientScan;

public sealed class AddressPatchFix : IClientFix
{
    public const string FixId = "address-patch";

    public string Id => FixId;

    public string Title => "Point the client at this server";

    public bool CanRestore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.ExecutablePath is { } executable && File.Exists(executable + ClientAddressPatch.BackupSuffix);
    }

    public FixOutcome Apply(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var executable = context.ExecutablePath ?? throw new HostingException("There is no client program in this folder.");
        if (context.ServerAddress is null || context.LobbyPort == 0)
        {
            throw new HostingException("The server address is not set yet, so the client cannot be pointed at it. Finish the network step of the setup first.");
        }

        var result = ClientAddressPatch.Apply(executable, context.ServerAddress, context.LobbyPort);
        return result.Changed
            ? new FixOutcome(true, $"The client now connects to {result.Address}. The original is kept as {Path.GetFileName(executable)}{ClientAddressPatch.BackupSuffix}.")
            : new FixOutcome(false, $"The client already connects to {result.Address}.");
    }

    public FixOutcome Restore(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var executable = context.ExecutablePath ?? throw new HostingException("There is no client program in this folder.");
        var backup = executable + ClientAddressPatch.BackupSuffix;
        if (!File.Exists(backup))
        {
            return new FixOutcome(false, "There is no saved original of the client program.");
        }

        if (File.ReadAllBytes(backup).AsSpan().SequenceEqual(File.ReadAllBytes(executable)))
        {
            return new FixOutcome(false, "The client program is already the original.");
        }

        try
        {
            File.Copy(backup, executable, overwrite: true);
        }
        catch (IOException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientRunning, $"Could not restore {executable}: {ex.Message}", "Close the game client, then try again.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PlayFailure(PlayFailureReason.ClientNotWritable, $"Could not restore {executable}: {ex.Message}", "Give your account write access to the client folder.", ex);
        }

        return new FixOutcome(true, $"{Path.GetFileName(executable)} is the original again.");
    }
}
