using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.Common.Launcher;

public sealed record SigningKeyPair(string PrivateKey, string PublicKey);

public sealed record KeyLink(string From, string To, string Signature);

public enum SignatureVerdict
{
    Valid,
    Missing,
    Malformed,
    Forged,
    UnknownSigner,
}

public static class ManifestSignature
{
    public const string FormatName = "tposeidon-signature";
    public const int CurrentVersion = 1;
    public const int MaxLinks = 8;
    public const int MaxSignatureBytes = 32 * 1024;
    public const string SignatureFile = "manifest.sig";

    private const string P256Oid = "1.2.840.10045.3.1.7";
    private const string LinkPrefix = "tposeidon-key-link-1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static SigningKeyPair CreateKeyPair()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return new SigningKeyPair(Convert.ToBase64String(key.ExportPkcs8PrivateKey()), Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()));
    }

    public static bool IsPublicKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        using var key = TryImportPublic(text.Trim());
        return key is not null;
    }

    public static bool IsKeyPair(SigningKeyPair pair)
    {
        ArgumentNullException.ThrowIfNull(pair);
        try
        {
            using var key = ECDsa.Create();
            key.ImportPkcs8PrivateKey(Convert.FromBase64String(pair.PrivateKey), out _);
            return IsP256(key) && string.Equals(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo()), pair.PublicKey, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    public static byte[] Sign(ReadOnlySpan<byte> manifest, SigningKeyPair key, IReadOnlyList<KeyLink> rotations)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(rotations);
        if (rotations.Count > MaxLinks)
        {
            throw new InvalidDataException($"A signature can carry at most {MaxLinks} key changes.");
        }

        var links = rotations.Select(link => new LinkDocument(link.From, link.To, link.Signature)).ToList();
        var document = new Document(FormatName, CurrentVersion, key.PublicKey, Convert.ToBase64String(SignBytes(manifest, key)), links);
        return JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
    }

    public static KeyLink Endorse(SigningKeyPair current, string newPublicKey)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (!IsPublicKey(newPublicKey))
        {
            throw new InvalidDataException("The new public key is not a P-256 key.");
        }

        return new KeyLink(current.PublicKey, newPublicKey, Convert.ToBase64String(SignBytes(LinkPayload(current.PublicKey, newPublicKey), current)));
    }

    public static SignatureVerdict Verify(ReadOnlySpan<byte> manifest, ReadOnlySpan<byte> signatureFile, string trustedPublicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trustedPublicKey);
        if (signatureFile.IsEmpty)
        {
            return SignatureVerdict.Missing;
        }

        if (signatureFile.Length > MaxSignatureBytes)
        {
            return SignatureVerdict.Malformed;
        }

        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(signatureFile, JsonOptions);
        }
        catch (JsonException)
        {
            return SignatureVerdict.Malformed;
        }

        if (document is null || document.Format != FormatName || document.Version != CurrentVersion || document.PublicKey is null || document.Signature is null)
        {
            return SignatureVerdict.Malformed;
        }

        var links = document.Rotations ?? [];
        if (links.Count > MaxLinks)
        {
            return SignatureVerdict.Malformed;
        }

        var trusted = trustedPublicKey.Trim();
        if (!string.Equals(document.PublicKey, trusted, StringComparison.Ordinal) && !Follows(trusted, document.PublicKey, links))
        {
            return SignatureVerdict.UnknownSigner;
        }

        return VerifyBytes(manifest, document.Signature, document.PublicKey) ? SignatureVerdict.Valid : SignatureVerdict.Forged;
    }

    public static string? SignerOf(ReadOnlySpan<byte> signatureFile)
    {
        if (signatureFile.IsEmpty || signatureFile.Length > MaxSignatureBytes)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Document>(signatureFile, JsonOptions)?.PublicKey;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Describe(SignatureVerdict verdict) => verdict switch
    {
        SignatureVerdict.Valid => "The server's updates are signed by its owner.",
        SignatureVerdict.Missing => "The server's update information is not signed.",
        SignatureVerdict.Malformed => "The signature on the server's update information is damaged.",
        SignatureVerdict.Forged => "The signature on the server's update information does not match its content.",
        _ => "The server's update information is signed with a key this launcher does not know.",
    };

    private static bool Follows(string trusted, string signer, IReadOnlyList<LinkDocument> links)
    {
        var current = trusted;
        for (var step = 0; step < MaxLinks; step++)
        {
            var link = links.FirstOrDefault(each => string.Equals(each.From, current, StringComparison.Ordinal));
            if (link?.To is null || link.Signature is null || !VerifyBytes(LinkPayload(current, link.To), link.Signature, current))
            {
                return false;
            }

            current = link.To;
            if (string.Equals(current, signer, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] LinkPayload(string from, string to) =>
        Encoding.UTF8.GetBytes(LinkPrefix + "\n" + from + "\n" + to);

    private static byte[] SignBytes(ReadOnlySpan<byte> data, SigningKeyPair key)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(Convert.FromBase64String(key.PrivateKey), out _);
        return ecdsa.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static bool VerifyBytes(ReadOnlySpan<byte> data, string signature, string publicKey)
    {
        using var key = TryImportPublic(publicKey);
        if (key is null)
        {
            return false;
        }

        try
        {
            return key.VerifyData(data, Convert.FromBase64String(signature), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    private static ECDsa? TryImportPublic(string publicKey)
    {
        var key = ECDsa.Create();
        try
        {
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKey), out _);
            if (IsP256(key))
            {
                return key;
            }
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            key.Dispose();
            return null;
        }

        key.Dispose();
        return null;
    }

    private static bool IsP256(ECDsa key) =>
        string.Equals(key.ExportParameters(false).Curve.Oid.Value, P256Oid, StringComparison.Ordinal);

    private sealed record LinkDocument(string? From, string? To, string? Signature);

    private sealed record Document(string? Format, int Version, string? PublicKey, string? Signature, IReadOnlyList<LinkDocument>? Rotations);
}
