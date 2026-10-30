using tPoseidon.Core.Crypto.S2;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientProgramCheck : IClientCheck
{
    public const string CheckId = "program";

    public string Id => CheckId;

    public bool ReadsWorldData => false;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Directory.Exists(context.ClientFolder))
        {
            return [ScanFinding.Problem(Id, $"The folder {context.ClientFolder} does not exist.")];
        }

        if (context.ExecutablePath is not { } executable)
        {
            return [ScanFinding.Problem(Id, "There is no Trickster.bin or Trickster.exe in this folder. Pick the folder you installed the game into.")];
        }

        var name = Path.GetFileName(executable);
        var bytes = File.ReadAllBytes(executable);
        if (ClientAddressPatch.Slots.Any(slot => ClientAddressPatch.FindSlot(bytes, slot) < 0))
        {
            return [ScanFinding.Problem(Id, $"{name} is not the KR 2014 client tPoseidon knows: the lobby addresses it connects to were not found. Use the 2014.01.28 KR client, or reinstall it.")];
        }

        var extraction = KeyTableExtractor.Extract(bytes);
        if (extraction.Table is null)
        {
            return [ScanFinding.Problem(Id, $"The packet key table could not be read from {name}: {extraction.Outcome} Use an unmodified KR 2014 client.")];
        }

        return [ScanFinding.Pass(Id, $"{name} is the KR 2014 client and its packet key table reads cleanly.")];
    }
}
