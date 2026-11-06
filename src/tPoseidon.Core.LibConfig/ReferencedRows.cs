using System.Security.Cryptography;
using System.Text.Json;

namespace tPoseidon.Core.LibConfig;

public static class ReferencedRows
{
    public const string Id = "referenced-rows";
    public const string Title = "Add the item, skill and drop rows that the client data names but does not define";
    private const string ResourceName = "ReferencedRows.json";
    private const int FingerprintLength = 8;

    private static readonly Lazy<(IReadOnlyList<LibConfigRowPatch> Patches, string Fingerprint)> Loaded = new(Load);

    public static IReadOnlyList<LibConfigRowPatch> Patches => Loaded.Value.Patches;

    public static string Fingerprint => Loaded.Value.Fingerprint;

    public static byte[] Merge(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        foreach (var patch in Patches)
        {
            xml = LibConfigXmlRowEditor.AddMissingRows(xml, patch).Xml;
        }

        return xml;
    }

    private static (IReadOnlyList<LibConfigRowPatch>, string) Load()
    {
        using var stream = typeof(ReferencedRows).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The resource {ResourceName} is missing from {typeof(ReferencedRows).Assembly.GetName().Name}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(bytes))[..FingerprintLength].ToLowerInvariant();
        using var document = JsonDocument.Parse(bytes);
        var patches = new List<LibConfigRowPatch>();
        foreach (var patch in document.RootElement.EnumerateArray())
        {
            var fields = patch.GetProperty("fields").EnumerateArray().Select(field => field.GetString()!).ToList();
            var rows = patch.GetProperty("rows").EnumerateArray()
                .Select(row => (IReadOnlyList<string>)row.EnumerateArray().Select(value => value.GetString()!).ToList())
                .ToList();
            patches.Add(new LibConfigRowPatch(patch.GetProperty("table").GetString()!, patch.GetProperty("keyField").GetString()!, fields, rows));
        }

        return (patches, fingerprint);
    }
}
