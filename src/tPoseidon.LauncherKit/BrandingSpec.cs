using System.Text.Json;
using System.Text.Json.Serialization;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public sealed record BrandingSpec(
    string ServerName,
    string Address,
    string WebUrl,
    string WindowTitle,
    string AccentColor,
    string Logo,
    string? Background,
    string Icon,
    string? Website,
    string? Discord,
    string? Welcome,
    string Language,
    string? PublicKey,
    bool TestOnThisComputer = false)
{
    public const string FormatName = "tposeidon-branding";
    public const int CurrentVersion = 1;
    public const string FolderName = "branding";
    public const string FileName = "branding.json";
    public const int MaxTitleLength = 80;
    public const int MaxLinkLength = 200;
    public const int MaxWelcomeLength = 500;
    public const int MaxPublicKeyLength = 256;
    public const long MaxLogoBytes = 4L << 20;
    public const long MaxBackgroundBytes = 8L << 20;
    public const long MaxIconBytes = 1L << 20;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string[] PictureExtensions = [".png", ".jpg", ".jpeg"];
    private static readonly string[] Languages = [ClientFolder.AutomaticService, ClientFolder.KoreanService, ClientFolder.EnglishService];

    public ServerFile Server => new(ServerName, Address, WebUrl, Welcome, AccentColor);

    public static BrandingSpec Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        Document? document;
        try
        {
            document = JsonSerializer.Deserialize<Document>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new HostingException($"The launcher branding is not valid: {ex.Message}", ex);
        }

        if (document is null || !string.Equals(document.Format, FormatName, StringComparison.Ordinal))
        {
            throw new HostingException("This is not a tPoseidon launcher branding file.");
        }

        if (document.Version != CurrentVersion)
        {
            throw new HostingException($"This branding is version {document.Version}, and this launcher reads version {CurrentVersion}.", "Build the launcher again with the control panel.", null);
        }

        return new BrandingSpec(
            document.ServerName ?? "",
            document.Address ?? "",
            document.WebUrl ?? "",
            document.WindowTitle ?? "",
            document.AccentColor ?? "",
            document.Logo ?? "",
            document.Background,
            document.Icon ?? "",
            document.Website,
            document.Discord,
            document.Welcome,
            document.Language ?? "",
            document.PublicKey,
            document.TestOnThisComputer == true).Validated();
    }

    public string ToJson()
    {
        var valid = Validated();
        return JsonSerializer.Serialize(
            new Document(FormatName, CurrentVersion, valid.ServerName, valid.Address, valid.WebUrl, valid.WindowTitle, valid.AccentColor, valid.Logo, valid.Background, valid.Icon, valid.Website, valid.Discord, valid.Welcome, valid.Language, valid.PublicKey, valid.TestOnThisComputer ? true : null),
            JsonOptions);
    }

    public BrandingSpec Validated()
    {
        var name = (ServerName ?? "").Trim();
        var title = (WindowTitle ?? "").Trim();
        if (title.Length == 0 || title.Length > MaxTitleLength)
        {
            throw new HostingException($"The window title needs 1 to {MaxTitleLength} characters.");
        }

        var server = new ServerFile(name, Address, WebUrl, Welcome, AccentColor).Validated();
        if (server.WebUrl is null)
        {
            throw new HostingException("The launcher needs the server's web address.", "Use something like http://play.example.org:9990.", null);
        }

        if (server.Color is null)
        {
            throw new HostingException("The launcher needs an accent colour like #3B82F6.");
        }

        if (!Languages.Contains(Language, StringComparer.Ordinal))
        {
            throw new HostingException($"'{Language}' is not a client language.", $"Use one of: {string.Join(", ", Languages)}.", null);
        }

        var publicKey = string.IsNullOrWhiteSpace(PublicKey) ? null : PublicKey.Trim();
        if (publicKey is { Length: > MaxPublicKeyLength })
        {
            throw new HostingException("The public key is too long.");
        }

        if (TestOnThisComputer && !string.Equals(server.Address, NetworkAddresses.Loopback, StringComparison.Ordinal))
        {
            throw new HostingException($"A test launcher only works with {NetworkAddresses.Loopback}.", "Use the address 127.0.0.1, or build a real launcher for players.", null);
        }

        return new BrandingSpec(
            server.Name,
            server.Address,
            server.WebUrl,
            title,
            server.Color,
            PictureName(Logo, "logo", PictureExtensions),
            string.IsNullOrWhiteSpace(Background) ? null : PictureName(Background, "background", PictureExtensions),
            PictureName(Icon, "icon", [".ico"]),
            Link(Website, "website"),
            Link(Discord, "Discord link"),
            server.Note,
            Language,
            publicKey,
            TestOnThisComputer);
    }

    private static string PictureName(string? name, string role, string[] extensions)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || trimmed != Path.GetFileName(trimmed) || trimmed.StartsWith('.') || trimmed.Length > 64
            || !extensions.Contains(Path.GetExtension(trimmed), StringComparer.OrdinalIgnoreCase))
        {
            throw new HostingException($"'{trimmed}' is not a usable {role} file name.", $"Use a plain file name ending in {string.Join(", ", extensions)}.", null);
        }

        return trimmed;
    }

    private static string? Link(string? value, string role)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Length > MaxLinkLength || !Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new HostingException($"'{text}' is not a {role}.", "Use a full address starting with https://.", null);
        }

        return text;
    }

    private sealed record Document(
        string? Format,
        int Version,
        string? ServerName,
        string? Address,
        string? WebUrl,
        string? WindowTitle,
        string? AccentColor,
        string? Logo,
        string? Background,
        string? Icon,
        string? Website,
        string? Discord,
        string? Welcome,
        string? Language,
        string? PublicKey,
        bool? TestOnThisComputer);
}
