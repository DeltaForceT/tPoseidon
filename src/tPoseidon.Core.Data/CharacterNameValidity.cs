namespace tPoseidon.Core.Data;

public static class CharacterNameValidity
{
    public const int FirstNonAscii = 128;
    public const char ExtraAsciiMark = '^';
    public const int FirstFullWidth = 0xFF01;
    public const int LastFullWidth = 0xFF5E;

    public static bool IsValid(string name, IEnumerable<string> forbiddenWords)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(forbiddenWords);
        if (forbiddenWords.Any(word => word.Length > 0 && name.Contains(word, StringComparison.OrdinalIgnoreCase))) return false;
        foreach (var c in name)
        {
            if (c < FirstNonAscii)
            {
                if (!IsAllowedAscii(c)) return false;
            }
            else if (c >= FirstFullWidth && c <= LastFullWidth)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsAllowedAscii(char c) =>
        c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' or ExtraAsciiMark;
}
