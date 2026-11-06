using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace tPoseidon.Core.LibConfig;

public static class ReferencedTables
{
    public const string Id = "referenced-tables";
    public const string Title = "Add the tables that the client data names but does not contain";
    private const string ResourceName = "ReferencedTables.json";
    private const int FingerprintLength = 8;

    private static readonly Lazy<(IReadOnlyList<LibConfigTableDefinition> Tables, string Fingerprint)> Loaded = new(Load);

    public static IReadOnlyList<LibConfigTableDefinition> Tables => Loaded.Value.Tables;

    public static string Fingerprint => Loaded.Value.Fingerprint;

    public static bool Pending(ReadOnlySpan<byte> xml) => LibConfigXmlTableAdder.MissingTables(xml, Tables).Count > 0;

    public static byte[] Merge(byte[] xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        return LibConfigXmlTableAdder.AddMissingTables(xml, Tables).Xml;
    }

    private static (IReadOnlyList<LibConfigTableDefinition>, string) Load()
    {
        using var stream = typeof(ReferencedTables).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The resource {ResourceName} is missing from {typeof(ReferencedTables).Assembly.GetName().Name}.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        var bytes = memory.ToArray();
        var fingerprint = Convert.ToHexString(SHA256.HashData(bytes))[..FingerprintLength].ToLowerInvariant();
        using var document = JsonDocument.Parse(bytes);
        var tables = new List<LibConfigTableDefinition>();
        foreach (var table in document.RootElement.EnumerateArray())
        {
            var fields = table.GetProperty("fields").EnumerateArray()
                .Select(field => new LibConfigFieldDefinition(field[0].GetString()!, field[1].GetInt32() != 0, field[2].GetString()!))
                .ToList();
            var rows = table.GetProperty("rows").EnumerateArray()
                .Select(row => (IReadOnlyList<string>)row.EnumerateArray().Select(value => value.GetString()!).ToList())
                .ToList();
            tables.Add(new LibConfigTableDefinition(table.GetProperty("name").GetString()!, table.GetProperty("source").GetString()!, table.GetProperty("infoId").GetInt32(), fields, rows));
        }

        return (tables, fingerprint);
    }
}
