namespace tPoseidon.Core.Net.Tests;

[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresDataFactAttribute : FactAttribute
{
    private static readonly string DataDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data"));

    public RequiresDataFactAttribute(params string[] files)
    {
        var missing = files.Where(file => !File.Exists(Path.Combine(DataDirectory, file))).ToArray();
        if (missing.Length > 0)
        {
            Skip = $"needs local reference data that is not part of the repository: {string.Join(", ", missing)}";
        }
    }
}
