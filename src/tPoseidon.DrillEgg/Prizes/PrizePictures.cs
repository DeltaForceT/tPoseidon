using System.Globalization;
using System.Net;
using System.Text;

namespace tPoseidon.DrillEgg.Prizes;

internal static class PrizePictureNames
{
    public static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".gif"] = "image/gif",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
    };

    private static readonly char[] Forbidden = ['/', '\\', ':', '*', '?', '"', '<', '>', '|'];

    public static bool IsPlainFileName(string name) =>
        !string.IsNullOrWhiteSpace(name)
        && name is not ("." or "..")
        && name.IndexOfAny(Forbidden) < 0
        && name.All(c => !char.IsControl(c));

    public static string? ContentTypeOf(string path) =>
        ContentTypes.TryGetValue(Path.GetExtension(path), out var type) ? type : null;
}

internal sealed class PrizePictures
{
    public const string PlaceholderType = "image/svg+xml";
    public const int Width = 130;
    public const int Height = 150;
    public const int LineLength = 14;
    public const int MaxLines = 3;

    private readonly string _folder;
    private readonly PrizeCatalog _catalog;

    public PrizePictures(string folder, PrizeCatalog catalog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = Path.GetFullPath(folder);
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public static string UrlOf(int itemId) => string.Create(CultureInfo.InvariantCulture, $"/gacha/picture/{itemId}");

    public string? Find(int itemId)
    {
        if (!Directory.Exists(_folder)) return null;
        if (_catalog.PictureOf(itemId) is { } named)
        {
            var path = Path.Combine(_folder, named);
            return File.Exists(path) && PrizePictureNames.ContentTypeOf(path) is not null ? path : null;
        }
        foreach (var stem in new[] { itemId.ToString(CultureInfo.InvariantCulture), _catalog.NameOf(itemId) })
        {
            if (!PrizePictureNames.IsPlainFileName(stem)) continue;
            foreach (var extension in PrizePictureNames.ContentTypes.Keys)
            {
                var path = Path.Combine(_folder, stem + extension);
                if (File.Exists(path)) return path;
            }
        }
        return null;
    }

    public string Placeholder(int itemId) => PlaceholderSvg(_catalog.NameOf(itemId));

    public static string PlaceholderSvg(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var lines = Wrap(name);
        var text = new StringBuilder();
        var top = 112 - ((lines.Count - 1) * 7);
        for (var i = 0; i < lines.Count; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"<text x=\"65\" y=\"{top + (i * 14)}\">{WebUtility.HtmlEncode(lines[i])}</text>");
        }
        return string.Create(CultureInfo.InvariantCulture, $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{Width}" height="{Height}" viewBox="0 0 {Width} {Height}">
            <defs>
            <linearGradient id="card" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff8e1"/><stop offset="1" stop-color="#f6cf7a"/></linearGradient>
            <radialGradient id="egg" cx="0.38" cy="0.32" r="0.75"><stop offset="0" stop-color="#ffffff"/><stop offset="0.55" stop-color="#ffe9a8"/><stop offset="1" stop-color="#e3a43c"/></radialGradient>
            </defs>
            <rect x="3" y="3" width="124" height="144" rx="14" fill="url(#card)" stroke="#a5651d" stroke-width="3"/>
            <rect x="9" y="9" width="112" height="132" rx="10" fill="none" stroke="#ffffff" stroke-opacity="0.7" stroke-width="1.5"/>
            <path d="M65 26 C83 26 95 50 95 68 C95 84 82 94 65 94 C48 94 35 84 35 68 C35 50 47 26 65 26 Z" fill="url(#egg)" stroke="#b9772a" stroke-width="2.5"/>
            <path d="M40 66 L50 58 L58 67 L66 57 L74 67 L82 58 L91 66" fill="none" stroke="#b9772a" stroke-width="2.5" stroke-linejoin="round"/>
            <path d="M102 24 l3 7 7 3 -7 3 -3 7 -3 -7 -7 -3 7 -3 z" fill="#ffffff"/>
            <path d="M26 92 l2 4 4 2 -4 2 -2 4 -2 -4 -4 -2 4 -2 z" fill="#ffffff"/>
            <g font-family="Verdana, Arial, sans-serif" font-size="11" font-weight="bold" fill="#6b3d0e" text-anchor="middle">{text}</g>
            </svg>
            """);
    }

    private static List<string> Wrap(string name)
    {
        var lines = new List<string>();
        var line = string.Empty;
        foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var piece = word.Length > LineLength ? word[..LineLength] : word;
            if (line.Length == 0) line = piece;
            else if (line.Length + 1 + piece.Length <= LineLength) line += " " + piece;
            else
            {
                lines.Add(line);
                line = piece;
            }
        }
        if (line.Length > 0) lines.Add(line);
        if (lines.Count > MaxLines)
        {
            lines = lines.Take(MaxLines).ToList();
            lines[^1] = lines[^1].Length >= LineLength ? lines[^1][..(LineLength - 1)] + "…" : lines[^1] + "…";
        }
        return lines;
    }
}
