using System.Text.Json;
using System.Text.Json.Serialization;

namespace tPoseidon.Core.Common.Launcher;

public sealed record NewsItem(string Title, string Date, string Text, string? Picture = null, bool Pinned = false);

public sealed record ManifestFile(string Path, long Size, string Sha256, string? Group = null);

public sealed record LauncherManifest(
    string Name,
    string? Color,
    string? Banner,
    string? Maintenance,
    string? LauncherVersion,
    IReadOnlyList<NewsItem> News,
    IReadOnlyList<ManifestFile> Files,
    string? BannerSha256 = null)
{
    public const string FormatName = "tposeidon-launcher";
    public const int CurrentVersion = 1;
    public const int MaxManifestBytes = 8 * 1024 * 1024;
    public const int MaxFiles = 20000;
    public const int MaxNews = 20;
    public const int MaxTitleLength = 120;
    public const int MaxTextLength = 4000;
    public const int MaxNameLength = 60;
    public const int MaxMaintenanceLength = 300;
    public const long MaxFileBytes = 1L << 30;
    public const long MaxTotalBytes = 8L << 30;
    public const long MaxBannerBytes = 2L << 20;
    public const long MaxNewsPictureBytes = 2L << 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public long TotalBytes => Files.Sum(file => file.Size);

    public static LauncherManifest Parse(ReadOnlySpan<byte> json)
    {
        if (json.Length > MaxManifestBytes)
        {
            throw new InvalidDataException("The launcher manifest is too big.");
        }

        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"The launcher manifest is not valid: {ex.Message}", ex);
        }

        if (document is null || !string.Equals(document.Format, FormatName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("This is not a tPoseidon launcher manifest.");
        }

        if (document.Version != CurrentVersion)
        {
            throw new InvalidDataException($"The launcher manifest is version {document.Version}, and this launcher reads version {CurrentVersion}.");
        }

        var manifest = new LauncherManifest(
            document.Name ?? "",
            document.Color,
            document.Banner,
            document.Maintenance,
            document.LauncherVersion,
            document.News ?? [],
            document.Files ?? [],
            document.BannerSha256);
        manifest.Validate();
        return manifest;
    }

    public byte[] ToJson()
    {
        Validate();
        return JsonSerializer.SerializeToUtf8Bytes(new Document(FormatName, CurrentVersion, Name, Color, Banner, Maintenance, LauncherVersion, News, Files, BannerSha256), JsonOptions);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            throw new InvalidDataException($"The server name must be 1 to {MaxNameLength} characters.");
        }

        if (Color is not null && !IsColor(Color))
        {
            throw new InvalidDataException($"'{Color}' is not a colour like #3B82F6.");
        }

        if (Banner is not null && !ClientPath.TryNormalize(Banner, out _))
        {
            throw new InvalidDataException($"'{Banner}' is not a banner file name.");
        }

        if (BannerSha256 is not null && !IsSha256(BannerSha256))
        {
            throw new InvalidDataException("The banner has no valid SHA-256.");
        }

        if (Maintenance is { Length: > MaxMaintenanceLength })
        {
            throw new InvalidDataException("The maintenance message is too long.");
        }

        if (News.Count > MaxNews)
        {
            throw new InvalidDataException($"A manifest can carry at most {MaxNews} news items.");
        }

        foreach (var item in News)
        {
            if (string.IsNullOrWhiteSpace(item.Title) || item.Title.Length > MaxTitleLength || item.Text is null || item.Text.Length > MaxTextLength || !DateOnly.TryParseExact(item.Date, "yyyy-MM-dd", out _))
            {
                throw new InvalidDataException("A news item needs a title, a yyyy-MM-dd date and text within the size limits.");
            }

            if (item.Picture is not null && !NewsStore.IsPictureName(item.Picture))
            {
                throw new InvalidDataException($"'{item.Picture}' is not a news picture file name.");
            }
        }

        if (Files.Count > MaxFiles)
        {
            throw new InvalidDataException($"A manifest can list at most {MaxFiles} files.");
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        foreach (var file in Files)
        {
            if (!ClientPath.TryNormalize(file.Path, out _))
            {
                throw new InvalidDataException($"'{file.Path}' is not a path inside the client folder.");
            }

            if (!seen.Add(file.Path))
            {
                throw new InvalidDataException($"'{file.Path}' is listed twice.");
            }

            if (file.Size < 0 || file.Size > MaxFileBytes)
            {
                throw new InvalidDataException($"'{file.Path}' is larger than the {MaxFileBytes >> 20} MB limit.");
            }

            if (!IsSha256(file.Sha256))
            {
                throw new InvalidDataException($"'{file.Path}' has no valid SHA-256.");
            }

            total += file.Size;
            if (total > MaxTotalBytes)
            {
                throw new InvalidDataException($"The listed files are larger than the {MaxTotalBytes >> 30} GB limit.");
            }
        }
    }

    public static bool IsSha256(string? text) =>
        text is { Length: 64 } && text.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool IsColor(string text) =>
        text.Length == 7 && text[0] == '#' && text.Skip(1).All(Uri.IsHexDigit);

    private sealed record Document(
        string? Format,
        int Version,
        string? Name,
        string? Color,
        string? Banner,
        string? Maintenance,
        string? LauncherVersion,
        IReadOnlyList<NewsItem>? News,
        IReadOnlyList<ManifestFile>? Files,
        string? BannerSha256);
}
