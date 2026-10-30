using System.Buffers.Binary;
using tPoseidon.Core.Common;
using tPoseidon.Core.Data;

namespace tPoseidon.Core.Net.S2;

public enum ChannelLoginRefusal : uint
{
    None = 0,
    VersionMissing = 0x3F1,
    VersionMismatch = 0x3F2,
    DuplicateUser = 0x3F3,
    AccountNotFound = 60100,
    InvalidAuthcode = 60101,
    InvalidAccountState = 60102,
}

public readonly record struct ClientVersion(ushort Major, ushort Minor)
{
    public const int WireBytes = 2 * sizeof(ushort);
    public const ushort WireMask = 0xA2D6;

    public static readonly ClientVersion Kr2014 = FromWire(new byte[] { 0xCF, 0xB2, 0x97, 0xB2 });

    public static ClientVersion FromWire(ReadOnlySpan<byte> wire)
    {
        if (wire.Length != WireBytes)
        {
            throw new InvalidPacketException($"Client version must be {WireBytes} bytes, got {wire.Length}.");
        }

        return new ClientVersion(
            (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(wire) ^ WireMask),
            (ushort)(BinaryPrimitives.ReadUInt16LittleEndian(wire[sizeof(ushort)..]) ^ WireMask));
    }

    public byte[] ToWire()
    {
        var wire = new byte[WireBytes];
        BinaryPrimitives.WriteUInt16LittleEndian(wire, (ushort)(Major ^ WireMask));
        BinaryPrimitives.WriteUInt16LittleEndian(wire.AsSpan(sizeof(ushort)), (ushort)(Minor ^ WireMask));
        return wire;
    }

    public static bool TryParse(string? text, out ClientVersion version)
    {
        version = default;
        var parts = text?.Split('.', StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 }
            || !ushort.TryParse(parts[0], out var major)
            || !ushort.TryParse(parts[1], out var minor))
        {
            return false;
        }

        version = new ClientVersion(major, minor);
        return true;
    }

    public override string ToString() => $"{Major}.{Minor}";
}

public readonly record struct ChannelLoginToken(uint AccountId, long Authcode)
{
    public const int Bytes = 12;

    public static ChannelLoginToken Read(ReadOnlySpan<byte> token)
    {
        if (token.Length != Bytes)
        {
            throw new InvalidPacketException($"Channel login token must be {Bytes} bytes, got {token.Length}.");
        }

        return new ChannelLoginToken(
            BinaryPrimitives.ReadUInt32LittleEndian(token),
            BinaryPrimitives.ReadInt64LittleEndian(token[4..]));
    }
}

public static class ChannelLogin
{
    public const ushort RefusalOpcode = 0x07D1;
    public const short ServerSelectedState = 3;
    public const short World = 1;
    public const short Island = 1;

    public static ChannelLoginRefusal CheckVersion(ClientVersion sent, ClientVersion expected)
    {
        if (sent.Major == 0 && sent.Minor == 0)
        {
            return ChannelLoginRefusal.VersionMissing;
        }

        return sent == expected ? ChannelLoginRefusal.None : ChannelLoginRefusal.VersionMismatch;
    }

    public static ChannelLoginRefusal Check(ChannelLoginToken token, AccountGameLogin? login)
    {
        if (login is null || login.AccountId != token.AccountId)
        {
            return ChannelLoginRefusal.AccountNotFound;
        }

        if (login.Authcode != token.Authcode)
        {
            return ChannelLoginRefusal.InvalidAuthcode;
        }

        return login.State == ServerSelectedState ? ChannelLoginRefusal.None : ChannelLoginRefusal.InvalidAccountState;
    }

    public static byte[] RefusalPayload(ChannelLoginRefusal refusal)
    {
        var payload = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)refusal);
        return payload;
    }
}
