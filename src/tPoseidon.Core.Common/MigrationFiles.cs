namespace tPoseidon.Core.Common;

public static class MigrationFiles
{
    public static bool IsNumbered(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        return fileName.Length >= 4
            && char.IsDigit(fileName[0])
            && char.IsDigit(fileName[1])
            && char.IsDigit(fileName[2])
            && fileName[3] == '_'
            && fileName.EndsWith(".sql", StringComparison.OrdinalIgnoreCase);
    }
}
