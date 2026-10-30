using System.Text;

namespace tPoseidon.Hosting;

public sealed record ClientSetupProfile(string Company, string Name)
{
    public const string English = "Trickster_gsp";
    public const string Korean = "Trickster_NT";
    public const string Software = "Software";
    public const string Section = "Setup";

    private static readonly byte[] SoftwareMarker = Encoding.ASCII.GetBytes("SOFTWARE\0");
    private static readonly byte[] PathPattern = Encoding.ASCII.GetBytes(@"%s\%s" + "\0");

    public string KeyPath => $@"{Software}\{Company}\{Name}\{Section}";

    public static string NameFor(string service) =>
        string.Equals(service, ClientFolder.EnglishService, StringComparison.OrdinalIgnoreCase) ? English : Korean;

    public static string? CompanyIn(ReadOnlySpan<byte> executable)
    {
        var offset = 0;
        while (true)
        {
            var found = executable[offset..].IndexOf(SoftwareMarker);
            if (found < 0)
            {
                return null;
            }

            var start = SkipZeros(executable, offset + found + SoftwareMarker.Length);
            var end = start;
            while (end < executable.Length && char.IsAsciiLetterOrDigit((char)executable[end]))
            {
                end++;
            }

            if (end > start && end < executable.Length && executable[end] == 0 && executable[SkipZeros(executable, end)..].StartsWith(PathPattern))
            {
                return Encoding.ASCII.GetString(executable[start..end]);
            }

            offset += found + SoftwareMarker.Length;
        }
    }

    private static int SkipZeros(ReadOnlySpan<byte> bytes, int index)
    {
        while (index < bytes.Length && bytes[index] == 0)
        {
            index++;
        }

        return index;
    }
}
