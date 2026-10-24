using tPoseidon.Hosting.Database;

namespace tPoseidon.Hosting;

public interface IAccountDirectory
{
    string? Find(string username);

    IReadOnlyList<string> Names();
}

public sealed class DatabaseAccountDirectory : IAccountDirectory
{
    private readonly DatabaseSettings _database;

    public DatabaseAccountDirectory(DatabaseSettings database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public string? Find(string username) => DatabaseAdmin.FindAccountName(_database, username);

    public IReadOnlyList<string> Names() => DatabaseAdmin.ListAccountNames(_database);
}

public static class PlayAccount
{
    public static string Choose(HostSettings settings, IAccountDirectory directory, string? username)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(directory);
        var name = username?.Trim() ?? "";
        if (name.Length == 0)
        {
            throw new HostingException("Say which account Play should log in with.");
        }

        var found = directory.Find(name)
            ?? throw new HostingException($"There is no account named '{name}'.", "Create it first with tposeidon setup, or pick an account that exists.", null);
        settings.Launch.Account = found;
        return found;
    }
}
