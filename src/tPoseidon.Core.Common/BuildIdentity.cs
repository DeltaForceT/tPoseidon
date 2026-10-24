using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace tPoseidon.Core.Common;

public sealed record BuildIdentity(string Version, string? Commit, string? Tag)
{
    public const string TagMetadataKey = "BuildTag";
    public const int TagLength = 16;
    private const int ShortCommitLength = 7;
    private const string TagSalt = "5ef32323077d09df68f32cff9e35efaa";

    public static BuildIdentity Current { get; } = FromAssembly(Assembly.GetEntryAssembly() ?? typeof(BuildIdentity).Assembly);

    public bool IsStamped => Tag is not null;

    public bool IsConsistent => Commit is not null && Tag is not null
        && string.Equals(ComputeTag(Version, Commit), Tag, StringComparison.Ordinal);

    public string ShortCommit => Commit is { Length: > ShortCommitLength } ? Commit[..ShortCommitLength] : Commit ?? string.Empty;

    public static BuildIdentity FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var tag = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, TagMetadataKey, StringComparison.Ordinal))?.Value;
        return Parse(string.IsNullOrWhiteSpace(informational) ? "0.0.0" : informational, string.IsNullOrWhiteSpace(tag) ? null : tag);
    }

    public static BuildIdentity Parse(string informationalVersion, string? tag = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(informationalVersion);
        var metadata = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        if (metadata < 0)
        {
            return new BuildIdentity(informationalVersion, null, tag);
        }

        var commit = informationalVersion[(metadata + 1)..];
        return new BuildIdentity(informationalVersion[..metadata], commit.Length == 0 ? null : commit, tag);
    }

    public static string ComputeTag(string version, string commit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(commit);
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(TagSalt + "\n" + version + "\n" + commit));
        return Convert.ToHexString(digest.AsSpan(0, TagLength / 2)).ToLowerInvariant();
    }

    public override string ToString()
    {
        if (Commit is null && Tag is null)
        {
            return Version;
        }

        var details = new StringBuilder();
        if (Commit is not null)
        {
            details.Append(ShortCommit);
        }

        if (Tag is not null)
        {
            if (details.Length > 0)
            {
                details.Append(", ");
            }

            details.Append("build ").Append(Tag);
        }

        return $"{Version} ({details})";
    }
}
