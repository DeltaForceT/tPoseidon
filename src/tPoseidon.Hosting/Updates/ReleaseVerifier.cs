using tPoseidon.Core.Common.Launcher;
using tPoseidon.Core.Common.Updates;

namespace tPoseidon.Hosting.Updates;

public static class ReleaseVerifier
{
    public static IReadOnlyList<ChecksumEntry> VerifyChecksumList(byte[] checksums, byte[] signature, string publicKey)
    {
        ArgumentNullException.ThrowIfNull(checksums);
        ArgumentNullException.ThrowIfNull(signature);
        var verdict = ManifestSignature.Verify(checksums, signature, publicKey);
        if (verdict != SignatureVerdict.Valid)
        {
            throw new HostingException(
                "The update was not installed: " + Reason(verdict),
                "Nothing was changed. Only an update signed with the tPoseidon release key is accepted.",
                null);
        }

        try
        {
            return ReleaseChecksums.Parse(checksums);
        }
        catch (InvalidDataException ex)
        {
            throw new HostingException("The update was not installed: " + ex.Message, "Nothing was changed.", ex);
        }
    }

    public static void VerifyPackage(string packagePath, string fileName, IReadOnlyList<ChecksumEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var expected = entries.FirstOrDefault(entry => string.Equals(entry.FileName, fileName, StringComparison.Ordinal))
            ?? throw new HostingException($"The update was not installed: the signed checksum list does not mention {fileName}.", "Nothing was changed.", null);
        var actual = ReleaseChecksums.HashOf(packagePath);
        if (!string.Equals(actual, expected.Sha256, StringComparison.Ordinal))
        {
            throw new HostingException($"The update was not installed: {fileName} does not match its signed checksum.", "The download is damaged or was changed. Nothing was changed.", null);
        }
    }

    private static string Reason(SignatureVerdict verdict) => verdict switch
    {
        SignatureVerdict.Missing => "the release has no signature.",
        SignatureVerdict.Malformed => "the signature is damaged.",
        SignatureVerdict.Forged => "the signature does not match the checksum list.",
        SignatureVerdict.UnknownSigner => "the release is signed with a key this panel does not trust.",
        _ => "the signature is not valid.",
    };
}
