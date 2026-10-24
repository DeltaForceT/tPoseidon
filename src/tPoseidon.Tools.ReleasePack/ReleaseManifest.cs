using System.Globalization;
using System.Text.Json;
using tPoseidon.Core.Common;

namespace tPoseidon.Tools.ReleasePack;

internal sealed record PackageEntry(string Path, long Bytes, string Sha256);

internal sealed record PackageResult(string Path, long Bytes, string Sha256, IReadOnlyList<PackageEntry> Entries);

internal sealed class ReleaseManifest
{
    public const string Schema = "tposeidon/release-manifest/1";
    public const string Product = "tPoseidon";
    public const string DrillEggProduct = "tPoseidon Drill Egg";
    public const string FilePrefix = "tposeidon";
    public const string DrillEggFilePrefix = "tposeidon-drill-egg";

    private readonly BuildIdentity _identity;
    private readonly IReadOnlyList<PackageResult> _packages;
    private readonly string _product;

    public ReleaseManifest(string product, BuildIdentity identity, IReadOnlyList<PackageResult> packages)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(product);
        _product = product;
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
    }

    public static string FileName(string prefix, string version) => $"{prefix}-{version}-manifest.json";

    public void Write(string path, DateTimeOffset builtAt)
    {
        using var file = File.Create(path);
        using var json = new Utf8JsonWriter(file, new JsonWriterOptions { Indented = true });
        json.WriteStartObject();
        json.WriteString("schema", Schema);
        json.WriteString("product", _product);
        json.WriteString("version", _identity.Version);
        json.WriteString("commit", _identity.Commit);
        json.WriteString("buildTag", _identity.Tag);
        json.WriteString("builtAt", builtAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture));
        json.WriteStartArray("packages");
        foreach (var package in _packages)
        {
            json.WriteStartObject();
            json.WriteString("file", Path.GetFileName(package.Path));
            json.WriteNumber("bytes", package.Bytes);
            json.WriteString("sha256", package.Sha256);
            json.WriteStartArray("entries");
            foreach (var entry in package.Entries)
            {
                json.WriteStartObject();
                json.WriteString("path", entry.Path);
                json.WriteNumber("bytes", entry.Bytes);
                json.WriteString("sha256", entry.Sha256);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        json.WriteEndArray();
        json.WriteEndObject();
        json.Flush();
        file.WriteByte((byte)'\n');
    }
}
