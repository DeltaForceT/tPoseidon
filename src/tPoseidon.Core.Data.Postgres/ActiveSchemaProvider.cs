namespace tPoseidon.Core.Data.Postgres;

public static class ActiveSchemaProvider
{
    private static volatile string _current = "public";

        public static string Current => _current;

        public static string Set(string? schema)
    {
        var resolved = string.IsNullOrWhiteSpace(schema) ? "public" : schema.Trim();
        var prev = _current;
        _current = resolved;
        return prev;
    }
}
