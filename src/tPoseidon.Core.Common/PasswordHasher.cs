using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;

namespace tPoseidon.Core.Common;

public static class PasswordHasher
{
    private const int SaltBytes = 16;
    private const int HashBytes = 32;
    private const int Iterations = 3;
    private const int MemoryKb = 65536;
    private const int DegreeOfParallelism = 4;
    private const string Prefix = "$argon2id$v=19$";

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = new byte[SaltBytes];
        RandomNumberGenerator.Fill(salt);

        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemoryKb,
        };
        var hash = argon2.GetBytes(HashBytes);

        return $"{Prefix}m={MemoryKb},t={Iterations},p={DegreeOfParallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string phc)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(phc);

        if (!phc.StartsWith(Prefix, StringComparison.Ordinal)) return false;

        var rest = phc.Substring(Prefix.Length);
        var parts = rest.Split('$');
        if (parts.Length != 3) return false;

        var paramsField = parts[0];
        var saltB64 = parts[1];
        var hashB64 = parts[2];

        if (!TryParseParams(paramsField, out var memoryKb, out var iterations, out var parallelism))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(saltB64);
            expectedHash = Convert.FromBase64String(hashB64);
        }
        catch (FormatException)
        {
            return false;
        }

        var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = parallelism,
            Iterations = iterations,
            MemorySize = memoryKb,
        };
        var actualHash = argon2.GetBytes(expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static bool TryParseParams(string s, out int memoryKb, out int iterations, out int parallelism)
    {
        memoryKb = 0;
        iterations = 0;
        parallelism = 0;

        foreach (var token in s.Split(','))
        {
            var idx = token.IndexOf('=');
            if (idx <= 0 || idx == token.Length - 1) return false;
            var key = token.Substring(0, idx);
            var value = token.Substring(idx + 1);
            if (!int.TryParse(value, out var n) || n <= 0) return false;
            switch (key)
            {
                case "m": memoryKb = n; break;
                case "t": iterations = n; break;
                case "p": parallelism = n; break;
                default: return false;
            }
        }

        return memoryKb > 0 && iterations > 0 && parallelism > 0;
    }
}
