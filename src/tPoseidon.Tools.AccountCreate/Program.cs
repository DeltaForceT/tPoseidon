using tPoseidon.Core.Common;
using tPoseidon.Core.Data.Postgres;

namespace tPoseidon.Tools.AccountCreate;

internal static class Program
{
    private const string LogTag = "[mt-account-create]";

    public static int Main(string[] args)
    {
        string? username = null;
        string? password = null;
        string? variant = null;
        var authority = "A";

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--username" or "-u":
                    if (++i >= args.Length) return Usage("missing value for --username");
                    username = args[i];
                    break;
                case "--password" or "-p":
                    if (++i >= args.Length) return Usage("missing value for --password");
                    password = args[i];
                    break;
                case "--authority" or "-a":
                    if (++i >= args.Length) return Usage("missing value for --authority");
                    authority = args[i];
                    break;
                case "--variant" or "-v":
                    if (++i >= args.Length) return Usage("missing value for --variant");
                    variant = args[i];
                    break;
                case "--help" or "-h":
                    return Usage(null);
                default:
                    return Usage($"unknown argument '{args[i]}'");
            }
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return Usage("--username is required");
        }
        if (string.IsNullOrEmpty(password))
        {
            password = ReadPasswordSilently();
            if (string.IsNullOrEmpty(password))
            {
                return Usage("password cannot be empty");
            }
        }
        if (authority is not ("A" or "B" or "Y" or "C"))
        {
            return Usage($"--authority must be A, B, Y, or C (got '{authority}')");
        }
        if (!string.IsNullOrWhiteSpace(variant))
        {
            var schema = variant.Trim().ToLowerInvariant() switch
            {
                "en2010" or "en" => "public",
                "kr2014" or "kr" => "kr",
                _ => null,
            };
            if (schema is null)
            {
                return Usage($"--variant must be 'en2010' or 'kr2014' (got '{variant}')");
            }
            Environment.SetEnvironmentVariable("MT_DB_SCHEMA", schema);
        }

        var connectionString = PostgresAccountStore.ResolveConnectionString();
        Console.WriteLine($"{LogTag} target: {SanitizeConnString(connectionString)}");

        var bootstrap = PostgresStoreBootstrap.InitializeAsync().GetAwaiter().GetResult();
        try
        {
            Console.WriteLine($"{LogTag} hashing password (Argon2id)...");
            var hash = PasswordHasher.Hash(password);

            Console.WriteLine($"{LogTag} inserting account '{username}' (authority={authority}) into schema '{ActiveSchemaProvider.Current}'...");
            var account = bootstrap.Store.RegisterAccount(username, hash, authority);

            Console.WriteLine($"{LogTag} OK -- new account_id = {account.AccountId}");
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine($"{LogTag} ERROR: {ex.Message}");
            return 2;
        }
        catch (Npgsql.NpgsqlException ex)
        {
            Console.Error.WriteLine($"{LogTag} DB ERROR: {ex.Message}");
            return 3;
        }
        finally
        {
            bootstrap.SchemaListener.DisposeAsync().AsTask().GetAwaiter().GetResult();
            bootstrap.DataSource.Dispose();
        }
    }

    private static int Usage(string? error)
    {
        if (error is not null)
        {
            Console.Error.WriteLine($"{LogTag} ERROR: {error}");
        }
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  mt-account-create --username <u> [--password <p>] [--authority A|B|Y|C] [--variant en2010|kr2014]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("If --password is omitted, you will be prompted (typed input is hidden).");
        Console.Error.WriteLine("Authority defaults to 'A' (normal). Use 'B' for GM, 'Y' for publisher, 'C' to pre-block.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("--variant maps to the Postgres schema the account is inserted into:");
        Console.Error.WriteLine("  en2010 (or 'en') → schema 'public'");
        Console.Error.WriteLine("  kr2014 (or 'kr') → schema 'kr'");
        Console.Error.WriteLine("If omitted, falls back to MT_DB_SCHEMA env var (or 'public' if unset).");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Connection target is read from PostgresAccountStore.ResolveConnectionString()");
        Console.Error.WriteLine("(env var MT_DB_CONNECTION, falling back to localhost:5433/modern_trickster).");
        return error is null ? 0 : 1;
    }

    private static string ReadPasswordSilently()
    {
        Console.Write("Password: ");
        var sb = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                break;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (sb.Length > 0) sb.Length--;
                continue;
            }
            if (!char.IsControl(key.KeyChar))
            {
                sb.Append(key.KeyChar);
            }
        }
        return sb.ToString();
    }

    private static string SanitizeConnString(string s)
    {
        var parts = s.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var kept = new List<string>();
        foreach (var part in parts)
        {
            var idx = part.IndexOf('=', StringComparison.Ordinal);
            if (idx <= 0) continue;
            var key = part.Substring(0, idx).Trim();
            if (key.Equals("Password", StringComparison.OrdinalIgnoreCase)) continue;
            kept.Add(part.Trim());
        }
        return string.Join(';', kept);
    }
}
