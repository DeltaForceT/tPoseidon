using System.Net.Sockets;
using Npgsql;

namespace tPoseidon.Hosting.Database;

public static class DatabaseErrors
{
    private const string InvalidPassword = "28P01";
    private const string InvalidAuthorization = "28000";
    private const string UnknownDatabase = "3D000";
    private const string InsufficientPrivilege = "42501";

    public static DatabaseException Explain(Exception error, NpgsqlConnectionStringBuilder target)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentNullException.ThrowIfNull(target);
        var where = $"{target.Host}:{target.Port}";
        var socket = FindSocketError(error);
        if (socket is not null)
        {
            return socket.SocketErrorCode switch
            {
                SocketError.ConnectionRefused => new DatabaseException(
                    DatabaseProblem.Unreachable,
                    $"Nothing answered on {where}. Either PostgreSQL is not installed, its service is not running, or it listens on another port.",
                    StartHint(),
                    error),
                SocketError.HostNotFound or SocketError.NoData => new DatabaseException(
                    DatabaseProblem.UnknownHost,
                    $"The database host '{target.Host}' could not be found. Check the name, or use localhost if PostgreSQL runs on this computer.",
                    null,
                    error),
                SocketError.TimedOut => TimedOut(where, error),
                _ => new DatabaseException(DatabaseProblem.Unreachable, $"Could not reach PostgreSQL on {where}: {socket.Message}", null, error),
            };
        }

        if (error is PostgresException postgres)
        {
            return postgres.SqlState switch
            {
                InvalidPassword => new DatabaseException(
                    DatabaseProblem.WrongPassword,
                    $"PostgreSQL rejected the password for login '{target.Username}'.",
                    PasswordHint(),
                    error),
                InvalidAuthorization => new DatabaseException(
                    DatabaseProblem.LoginRefused,
                    $"PostgreSQL refused login '{target.Username}' from this computer ({postgres.MessageText}).",
                    AuthenticationHint(),
                    error),
                UnknownDatabase => new DatabaseException(
                    DatabaseProblem.UnknownDatabase,
                    $"The database '{target.Database}' does not exist on {where}.",
                    null,
                    error),
                InsufficientPrivilege => new DatabaseException(
                    DatabaseProblem.NotAllowed,
                    $"Login '{target.Username}' is not allowed to do this: {postgres.MessageText}. Use an admin login such as 'postgres'.",
                    null,
                    error),
                _ => new DatabaseException(DatabaseProblem.Other, $"PostgreSQL error {postgres.SqlState}: {postgres.MessageText}", null, error),
            };
        }

        if (error is TimeoutException || error.InnerException is TimeoutException)
        {
            return TimedOut(where, error);
        }

        return new DatabaseException(DatabaseProblem.Other, $"Could not connect to PostgreSQL on {where}: {error.Message}", null, error);
    }

    public static DatabaseException TooOld(Version version, NpgsqlConnectionStringBuilder target, int minimum, int tested)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(target);
        return new DatabaseException(
            DatabaseProblem.TooOld,
            $"PostgreSQL {version.Major}.{version.Minor} on {target.Host}:{target.Port} is too old. tPoseidon needs PostgreSQL {minimum} or newer (we test on {tested}).",
            UpgradeHint(),
            null);
    }

    public static string StartHint()
    {
        if (OperatingSystem.IsWindows())
        {
            return "On Windows: open Services (services.msc) and start the 'postgresql-x64-<version>' service, or install PostgreSQL from postgresql.org.";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "On macOS: start Postgres.app, or run 'brew services start postgresql@16' if you installed it with Homebrew.";
        }

        return "On Linux: run 'sudo systemctl start postgresql' (and 'sudo systemctl enable postgresql' to start it at boot), or install it with your package manager.";
    }

    public static string UpgradeHint()
    {
        if (OperatingSystem.IsWindows())
        {
            return "Install a newer PostgreSQL from postgresql.org. It can run next to the old one on another port.";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "Run 'brew install postgresql@16', then use its port.";
        }

        return "Install a newer package, for example 'sudo apt install postgresql-16', then use its port.";
    }

    private static DatabaseException TimedOut(string where, Exception error) =>
        new(DatabaseProblem.TimedOut, $"Connecting to {where} timed out. A firewall may be blocking the port, or the host is down.", null, error);

    private static string PasswordHint()
    {
        if (OperatingSystem.IsWindows())
        {
            return "For the 'postgres' login this is the password you chose in the PostgreSQL installer.";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "Try 'psql -U postgres' to check the login. Homebrew installs often have no password: leave the field empty.";
        }

        return "Check that you can log in with 'sudo -u postgres psql', then set a password there with \\password postgres.";
    }

    private static string AuthenticationHint() =>
        OperatingSystem.IsLinux()
            ? "On Linux the 'postgres' login often only works through 'sudo -u postgres'. Give it a password with: sudo -u postgres psql -c \"ALTER USER postgres PASSWORD 'choose-one'\""
            : "Check the login name, and that pg_hba.conf allows password logins from this computer.";

    private static SocketException? FindSocketError(Exception error)
    {
        for (var current = error; current is not null; current = current.InnerException)
        {
            if (current is SocketException socket)
            {
                return socket;
            }
        }

        return null;
    }
}
