namespace tPoseidon.Core.Common.Launcher;

public sealed class OriginalFileGuard
{
    public static readonly OriginalFileGuard None = new([]);

    private readonly HashSet<string> _names;

    public OriginalFileGuard(IEnumerable<string> originalFileNames)
    {
        ArgumentNullException.ThrowIfNull(originalFileNames);
        _names = new HashSet<string>(originalFileNames, StringComparer.OrdinalIgnoreCase);
        foreach (var name in ClientPath.ProtectedNames)
        {
            _names.Add(name);
        }
    }

    public int Count => _names.Count;

    public static OriginalFileGuard FromClientFolder(string? clientFolder)
    {
        if (string.IsNullOrWhiteSpace(clientFolder) || !Directory.Exists(clientFolder))
        {
            return None;
        }

        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
        return new OriginalFileGuard(Directory.EnumerateFiles(clientFolder, "*", options).Select(Path.GetFileName).OfType<string>());
    }

    public bool Matches(string relativePath) =>
        _names.Contains(relativePath[(relativePath.LastIndexOf('/') + 1)..]);
}
