using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.Launcher;

public enum ChannelTrust
{
    Unknown,
    NoSignature,
    Verified,
}

public sealed class ManifestTrust
{
    private readonly string? _publicKey;

    public ManifestTrust(string? publicKey)
    {
        _publicKey = string.IsNullOrWhiteSpace(publicKey) ? null : publicKey.Trim();
    }

    public bool Required => _publicKey is not null;

    public ChannelTrust Check(ChannelManifest fetched)
    {
        ArgumentNullException.ThrowIfNull(fetched);
        var signature = fetched.Signature ?? [];
        if (_publicKey is not null)
        {
            var verdict = ManifestSignature.Verify(fetched.Json, signature, _publicKey);
            return verdict == SignatureVerdict.Valid
                ? ChannelTrust.Verified
                : throw Refuse(verdict);
        }

        if (signature.Length == 0)
        {
            return ChannelTrust.NoSignature;
        }

        var signer = ManifestSignature.SignerOf(signature);
        var own = signer is null ? SignatureVerdict.Malformed : ManifestSignature.Verify(fetched.Json, signature, signer);
        return own == SignatureVerdict.Valid ? ChannelTrust.Verified : throw Refuse(own);
    }

    private static PlayFailure Refuse(SignatureVerdict verdict) =>
        new(PlayFailureReason.UpdatesNotTrusted, ManifestSignature.Describe(verdict), "Nothing was downloaded. Ask the server owner for a new launcher, and do not play on this server until you did.", null);
}
