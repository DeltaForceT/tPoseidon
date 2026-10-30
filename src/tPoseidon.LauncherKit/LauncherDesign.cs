using System.Globalization;
using tPoseidon.Core.Common.Launcher;
using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public enum WizardStep
{
    Identity,
    Look,
    Connection,
    Links,
    Build,
}

public sealed record PresetAssets(byte[] Logo, byte[] Background, byte[] Icon);

public sealed class LauncherDesign
{
    public const string PresetAccent = "#5B9DFF";
    public const string PresetTitle = "tPoseidon Launcher";
    public const string DefaultAccent = "#3B82F6";

    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];
    private static readonly byte[] JpegMagic = [0xFF, 0xD8, 0xFF];

    private PlayerAudience _audience;
    private readonly bool _shopEnabled;
    private readonly int _lobbyPort;

    private LauncherDesign(PlayerAudience audience, bool shopEnabled, int lobbyPort)
    {
        _audience = audience;
        _shopEnabled = shopEnabled;
        _lobbyPort = lobbyPort;
    }

    public string ServerName { get; set; } = "";

    public string WindowTitle { get; set; } = "";

    public string AccentColor { get; set; } = DefaultAccent;

    public string Address { get; set; } = "";

    public int WebPort { get; set; }

    public string? Website { get; set; }

    public string? Discord { get; set; }

    public string? Welcome { get; set; }

    public string Language { get; set; } = ClientFolder.AutomaticService;

    public byte[]? Logo { get; set; }

    public byte[]? Background { get; set; }

    public byte[]? Icon { get; set; }

    public bool UsesTPoseidonLook { get; private set; }

    public bool TestOnThisComputer { get; private set; }

    public string? PublicKey { get; set; }

    public bool IsLocalOnly => _audience == PlayerAudience.Local;

    public bool NeedsReachableAddress => IsLocalOnly && !TestOnThisComputer;

    public string WebUrl => string.Create(CultureInfo.InvariantCulture, $"http://{Address.Trim()}:{WebPort}");

    public static LauncherDesign From(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new LauncherDesign(settings.Network.Audience, settings.ShopEnabled, settings.Ports.Lobby)
        {
            ServerName = settings.Launch.WorldName,
            WindowTitle = settings.Launch.WorldName,
            Address = settings.Network.PublicAddress,
            WebPort = settings.Ports.Shop,
        };
    }

    public void ApplyNetwork(HostSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _audience = settings.Network.Audience;
        Address = settings.Network.PublicAddress;
        WebPort = settings.Ports.Shop;
        TestOnThisComputer = false;
    }

    public void BuildTestForThisComputer()
    {
        if (!IsLocalOnly)
        {
            throw new HostingException("A test launcher is only for a server that accepts players on this computer alone.");
        }

        Address = NetworkAddresses.Loopback;
        TestOnThisComputer = true;
    }

    public void UseTPoseidonLook(PresetAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        Logo = assets.Logo;
        Background = assets.Background;
        Icon = assets.Icon;
        AccentColor = PresetAccent;
        WindowTitle = PresetTitle;
        UsesTPoseidonLook = true;
    }

    public void ChangeLook()
    {
        UsesTPoseidonLook = false;
    }

    public IReadOnlyList<string> Problems(WizardStep step)
    {
        var problems = new List<string>();
        switch (step)
        {
            case WizardStep.Identity:
                Identity(problems);
                break;
            case WizardStep.Look:
                Look(problems);
                break;
            case WizardStep.Connection:
                Connection(problems);
                break;
            case WizardStep.Links:
                Links(problems);
                break;
            default:
                foreach (var each in Enum.GetValues<WizardStep>().Where(each => each != WizardStep.Build))
                {
                    problems.AddRange(Problems(each));
                }

                break;
        }

        return problems;
    }

    public BrandingSpec ToSpec()
    {
        var problems = Problems(WizardStep.Build);
        if (problems.Count > 0)
        {
            throw new HostingException(problems[0]);
        }

        return new BrandingSpec(
            ServerName,
            Address,
            WebUrl,
            WindowTitle,
            AccentColor,
            "logo.png",
            Background is null ? null : "background" + PictureExtension(Background),
            "icon.ico",
            Website,
            Discord,
            Welcome,
            Language,
            PublicKey,
            TestOnThisComputer).Validated();
    }

    public LauncherBuildRequest ToRequest(string version, DateTimeOffset builtAt)
    {
        var spec = ToSpec() with { LauncherVersion = LauncherVersions.WithBuild(version, builtAt) };
        return new LauncherBuildRequest(spec, Logo!, Background, Icon!, version);
    }

    public Uri ManifestUrl() => new(new Uri(WebUrl), "/launcher/manifest.json");

    private static string PictureExtension(byte[] picture) =>
        picture.AsSpan().StartsWith(PngMagic) ? ".png" : ".jpg";

    private static bool IsPicture(byte[] picture) =>
        picture.AsSpan().StartsWith(PngMagic) || picture.AsSpan().StartsWith(JpegMagic);

    private void Identity(List<string> problems)
    {
        Run(problems, () => new ServerFile(ServerName, "x", null, null).Validated());
        if (string.IsNullOrWhiteSpace(WindowTitle) || WindowTitle.Trim().Length > BrandingSpec.MaxTitleLength)
        {
            problems.Add($"The window title needs 1 to {BrandingSpec.MaxTitleLength} characters.");
        }

        if (Logo is null)
        {
            problems.Add("Choose a logo (a PNG picture).");
        }
        else if (!Logo.AsSpan().StartsWith(PngMagic))
        {
            problems.Add("The logo must be a PNG picture.");
        }
        else if (Logo.Length > BrandingSpec.MaxLogoBytes)
        {
            problems.Add("The logo is too big (4 MB at most).");
        }

        if (Icon is null)
        {
            problems.Add("The icon could not be made from the logo.");
        }
    }

    private void Look(List<string> problems)
    {
        if (!LauncherManifest.IsColor(AccentColor ?? ""))
        {
            problems.Add($"'{AccentColor}' is not a colour like #3B82F6.");
        }

        if (Background is { } background)
        {
            if (!IsPicture(background))
            {
                problems.Add("The background must be a PNG or JPEG picture.");
            }
            else if (background.Length > BrandingSpec.MaxBackgroundBytes)
            {
                problems.Add("The background is too big (8 MB at most).");
            }
        }
    }

    private void Connection(List<string> problems)
    {
        if (WebPort is < 1 or > 65535)
        {
            problems.Add("The web port must be between 1 and 65535.");
            return;
        }

        var shared = new HostSettings { ShopEnabled = _shopEnabled };
        shared.Network.Audience = TestOnThisComputer ? PlayerAudience.Lan : _audience;
        shared.Network.PublicAddress = Address;
        shared.Ports.Shop = WebPort;
        shared.Ports.Lobby = _lobbyPort;
        if (TestOnThisComputer && !string.Equals(Address.Trim(), NetworkAddresses.Loopback, StringComparison.Ordinal))
        {
            problems.Add($"A test launcher only works with {NetworkAddresses.Loopback}.");
            return;
        }

        Run(problems, () => ServerFile.ForServer(shared, string.IsNullOrWhiteSpace(ServerName) ? "Server" : ServerName, WebUrl, null, null));
    }

    private void Links(List<string> problems)
    {
        Run(problems, () => new BrandingSpec("Server", "x", "http://x:1", "x", DefaultAccent, "l.png", null, "i.ico", Website, Discord, Welcome, Language, null).Validated());
    }

    private static void Run(List<string> problems, Action check)
    {
        try
        {
            check();
        }
        catch (HostingException ex)
        {
            problems.Add(ex.Hint is null ? ex.Summary : $"{ex.Summary} {ex.Hint}");
        }
    }
}

public sealed record LauncherBuildRequest(BrandingSpec Spec, byte[] Logo, byte[]? Background, byte[] Icon, string Version);
