using System.Runtime.Versioning;
using Microsoft.Win32;

namespace tPoseidon.Hosting.ClientScan;

public sealed class ClientRegistryCheck : IClientCheck
{
    public const string CheckId = "registry";
    public const string Use3DEffectValue = "Use3DEffect";

    public string Id => CheckId;

    public bool ReadsWorldData => false;

    public IReadOnlyList<ScanFinding> Run(ClientScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!OperatingSystem.IsWindows() || context.ExecutablePath is not { } executable)
        {
            return [];
        }

        var company = ClientSetupProfile.CompanyIn(File.ReadAllBytes(executable));
        if (company is null)
        {
            return [ScanFinding.Warning(Id, $"The registry name the client stores its settings under was not found in {Path.GetFileName(executable)}, so the launcher cannot show its game settings.")];
        }

        return [ReadUse3DEffect(new ClientSetupProfile(company, ClientSetupProfile.NameFor(context.Plan.Service)))];
    }

    [SupportedOSPlatform("windows")]
    private static ScanFinding ReadUse3DEffect(ClientSetupProfile profile)
    {
        using var key = Registry.CurrentUser.OpenSubKey(profile.KeyPath);
        if (key is null)
        {
            return ScanFinding.Pass(CheckId, "The client has not stored any settings yet; it creates them on first start.");
        }

        return key.GetValue(Use3DEffectValue) is 0
            ? ScanFinding.Warning(CheckId, "The client setting Use3DEffect is 0, so warp and portal effects do not show. Turn it on in the launcher's game settings.")
            : ScanFinding.Pass(CheckId, "The client settings in the registry look fine.");
    }
}
