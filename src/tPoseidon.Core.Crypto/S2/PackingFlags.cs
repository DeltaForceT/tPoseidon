using System.Diagnostics.CodeAnalysis;

namespace tPoseidon.Core.Crypto.S2;

[Flags]
[SuppressMessage("Naming", "CA1711", Justification = "Flags enum names ending in 'Flags' match the [Flags] attribute convention.")]
public enum PackingFlags : byte
{
    None                  = 0x00,

        KeyUpdate             = 0x01,

        EncryptionStateMarker = 0x02,

        PayloadEncrypted      = 0x04,

        DummyPadding          = 0x08,
}
