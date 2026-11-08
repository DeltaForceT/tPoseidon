using System.Formats.Asn1;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;

namespace tPoseidon.Core.Common.Launcher;

internal static class PortableEcdsa
{
    public const int CoordinateBytes = 32;
    public const string EcPublicKeyOid = "1.2.840.10045.2.1";
    public const string P256Oid = "1.2.840.10045.3.1.7";

    private static readonly BigInteger FieldPrime = Parse("FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF");
    private static readonly BigInteger Order = Parse("FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551");
    private static readonly BigInteger CurveB = Parse("5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B");
    private static readonly Point Generator = new(
        Parse("6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296"),
        Parse("4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5"));

    private readonly record struct Point(BigInteger X, BigInteger Y);

    public static bool IsPlatformFailure(Exception ex) =>
        ex is CryptographicException or PlatformNotSupportedException or DllNotFoundException or EntryPointNotFoundException or TypeInitializationException;

    public static (byte[] Pkcs8, byte[] PublicInfo) CreateKeyPair()
    {
        BigInteger d;
        var buffer = new byte[CoordinateBytes];
        do
        {
            RandomNumberGenerator.Fill(buffer);
            d = new BigInteger(buffer, isUnsigned: true, isBigEndian: true);
        }
        while (d < BigInteger.One || d >= Order);

        var q = Multiply(d, Generator)!.Value;
        return (EncodePrivate(d, q), EncodePublic(q));
    }

    public static byte[] PublicInfoOf(ReadOnlySpan<byte> pkcs8) => EncodePublic(Multiply(ReadScalar(pkcs8), Generator)!.Value);

    public static bool IsOnCurve(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y)
    {
        var px = new BigInteger(x, isUnsigned: true, isBigEndian: true);
        var py = new BigInteger(y, isUnsigned: true, isBigEndian: true);
        return px < FieldPrime && py < FieldPrime && Mod(py * py - (px * px * px - 3 * px + CurveB), FieldPrime).IsZero;
    }

    public static byte[] Sign(ReadOnlySpan<byte> pkcs8, ReadOnlySpan<byte> data)
    {
        var d = ReadScalar(pkcs8);
        var hash = PortableSha256.Hash(data);
        var z = new BigInteger(hash, isUnsigned: true, isBigEndian: true);
        var nonces = new Rfc6979(d, hash);
        while (true)
        {
            var k = nonces.Next();
            var r = Mod(Multiply(k, Generator)!.Value.X, Order);
            if (r.IsZero)
            {
                continue;
            }

            var s = Mod(Inverse(k, Order) * (z + r * d), Order);
            if (s.IsZero)
            {
                continue;
            }

            var signature = new byte[2 * CoordinateBytes];
            Fixed(r).CopyTo(signature, 0);
            Fixed(s).CopyTo(signature, CoordinateBytes);
            return signature;
        }
    }

    public static bool Verify(ReadOnlySpan<byte> x, ReadOnlySpan<byte> y, ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        if (signature.Length != 2 * CoordinateBytes || !IsOnCurve(x, y))
        {
            return false;
        }

        var r = new BigInteger(signature[..CoordinateBytes], isUnsigned: true, isBigEndian: true);
        var s = new BigInteger(signature[CoordinateBytes..], isUnsigned: true, isBigEndian: true);
        if (r < BigInteger.One || r >= Order || s < BigInteger.One || s >= Order)
        {
            return false;
        }

        var q = new Point(new BigInteger(x, isUnsigned: true, isBigEndian: true), new BigInteger(y, isUnsigned: true, isBigEndian: true));
        var z = new BigInteger(PortableSha256.Hash(data), isUnsigned: true, isBigEndian: true);
        var w = Inverse(s, Order);
        var sum = Add(Multiply(Mod(z * w, Order), Generator), Multiply(Mod(r * w, Order), q));
        return sum is { } point && Mod(point.X, Order) == r;
    }

    private static byte[] UncompressedPoint(Point q)
    {
        var point = new byte[1 + 2 * CoordinateBytes];
        point[0] = 4;
        Fixed(q.X).CopyTo(point, 1);
        Fixed(q.Y).CopyTo(point, 1 + CoordinateBytes);
        return point;
    }

    private static byte[] EncodePublic(Point q)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            WriteAlgorithm(writer);
            writer.WriteBitString(UncompressedPoint(q));
        }

        return writer.Encode();
    }

    private static byte[] EncodePrivate(BigInteger d, Point q)
    {
        var inner = new AsnWriter(AsnEncodingRules.DER);
        using (inner.PushSequence())
        {
            inner.WriteInteger(1);
            inner.WriteOctetString(Fixed(d));
            using (inner.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1)))
            {
                inner.WriteBitString(UncompressedPoint(q));
            }
        }

        var outer = new AsnWriter(AsnEncodingRules.DER);
        using (outer.PushSequence())
        {
            outer.WriteInteger(0);
            WriteAlgorithm(outer);
            outer.WriteOctetString(inner.Encode());
        }

        return outer.Encode();
    }

    private static void WriteAlgorithm(AsnWriter writer)
    {
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(EcPublicKeyOid);
            writer.WriteObjectIdentifier(P256Oid);
        }
    }

    private static BigInteger ReadScalar(ReadOnlySpan<byte> pkcs8)
    {
        try
        {
            var info = new AsnReader(pkcs8.ToArray(), AsnEncodingRules.BER).ReadSequence();
            info.ReadInteger();
            var algorithm = info.ReadSequence();
            if (algorithm.ReadObjectIdentifier() != EcPublicKeyOid || algorithm.ReadObjectIdentifier() != P256Oid)
            {
                throw new CryptographicException("The private key is not a P-256 key.");
            }

            var key = new AsnReader(info.ReadOctetString(), AsnEncodingRules.BER).ReadSequence();
            key.ReadInteger();
            var d = new BigInteger(key.ReadOctetString(), isUnsigned: true, isBigEndian: true);
            if (d < BigInteger.One || d >= Order)
            {
                throw new CryptographicException("The private key is out of range.");
            }

            return d;
        }
        catch (AsnContentException ex)
        {
            throw new CryptographicException("The private key is damaged.", ex);
        }
    }

    private static byte[] Fixed(BigInteger value)
    {
        var bytes = new byte[CoordinateBytes];
        var source = value.ToByteArray(isUnsigned: true, isBigEndian: true);
        source.CopyTo(bytes, CoordinateBytes - source.Length);
        return bytes;
    }

    private static BigInteger Parse(string hex) => BigInteger.Parse("0" + hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static BigInteger Mod(BigInteger value, BigInteger modulus)
    {
        var result = value % modulus;
        return result.Sign < 0 ? result + modulus : result;
    }

    private static BigInteger Inverse(BigInteger value, BigInteger modulus) => BigInteger.ModPow(Mod(value, modulus), modulus - 2, modulus);

    private static Point? Add(Point? left, Point? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        var a = left.Value;
        var b = right.Value;
        BigInteger slope;
        if (a.X == b.X)
        {
            if (Mod(a.Y + b.Y, FieldPrime).IsZero)
            {
                return null;
            }

            slope = Mod((3 * a.X * a.X - 3) * Inverse(2 * a.Y, FieldPrime), FieldPrime);
        }
        else
        {
            slope = Mod((b.Y - a.Y) * Inverse(b.X - a.X, FieldPrime), FieldPrime);
        }

        var x = Mod(slope * slope - a.X - b.X, FieldPrime);
        return new Point(x, Mod(slope * (a.X - x) - a.Y, FieldPrime));
    }

    private static Point? Multiply(BigInteger scalar, Point point)
    {
        Point? result = null;
        Point? addend = point;
        while (!scalar.IsZero)
        {
            if (!scalar.IsEven)
            {
                result = Add(result, addend);
            }

            addend = Add(addend, addend);
            scalar >>= 1;
        }

        return result;
    }

    private sealed class Rfc6979
    {
        private byte[] _k = new byte[PortableSha256.DigestBytes];
        private byte[] _v = new byte[PortableSha256.DigestBytes];

        public Rfc6979(BigInteger privateKey, byte[] hash)
        {
            Array.Fill(_v, (byte)1);
            var x = Fixed(privateKey);
            var h = Fixed(Mod(new BigInteger(hash, isUnsigned: true, isBigEndian: true), Order));
            _k = PortableSha256.Hmac(_k, [.. _v, 0, .. x, .. h]);
            _v = PortableSha256.Hmac(_k, _v);
            _k = PortableSha256.Hmac(_k, [.. _v, 1, .. x, .. h]);
            _v = PortableSha256.Hmac(_k, _v);
        }

        public BigInteger Next()
        {
            while (true)
            {
                _v = PortableSha256.Hmac(_k, _v);
                var candidate = new BigInteger(_v, isUnsigned: true, isBigEndian: true);
                var accepted = candidate >= BigInteger.One && candidate < Order;
                _k = PortableSha256.Hmac(_k, [.. _v, 0]);
                _v = PortableSha256.Hmac(_k, _v);
                if (accepted)
                {
                    return candidate;
                }
            }
        }
    }
}
