using System.Runtime.Versioning;
using System.Security;
using Microsoft.Win32;

namespace tPoseidon.Hosting;

public sealed record ClientResolution(int Width, int Height)
{
    public string Label => $"{Width} x {Height}";
}

public sealed record ClientDisplay(int Width, int Height, bool FullScreen)
{
    public static readonly IReadOnlyList<ClientResolution> Resolutions = [new(800, 600), new(1024, 768)];

    public static ClientDisplay Default { get; } = new(1024, 768, false);

    public ClientResolution Resolution => new(Width, Height);

    public bool IsOffered => Resolutions.Contains(Resolution);

    public static ClientDisplay Validated(ClientDisplay? display) =>
        display is { IsOffered: true } ? display : throw new HostingException("This resolution is not one the client offers.", "Pick 800 x 600 or 1024 x 768.", null);
}

[SupportedOSPlatform("windows")]
public static class RegistryClientDisplay
{
    public const string WidthValue = "widthPixel";
    public const string HeightValue = "heightPixel";
    public const string FullScreenValue = "Full Screen";

    public static ClientDisplay Read(ClientSetupProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var fallback = ClientDisplay.Default;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(profile.KeyPath);
            return key is null
                ? fallback
                : new ClientDisplay(Int(key, WidthValue, fallback.Width), Int(key, HeightValue, fallback.Height), Int(key, FullScreenValue, 0) != 0);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            throw new HostingException($"The client display settings could not be read: {ex.Message}", ex);
        }
    }

    public static void Write(ClientSetupProfile profile, ClientDisplay display)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ClientDisplay.Validated(display);
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(profile.KeyPath, writable: true);
            key.SetValue(WidthValue, display.Width, RegistryValueKind.DWord);
            key.SetValue(HeightValue, display.Height, RegistryValueKind.DWord);
            key.SetValue(FullScreenValue, display.FullScreen ? 1 : 0, RegistryValueKind.DWord);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            throw new HostingException($"The client display settings could not be saved: {ex.Message}", ex);
        }
    }

    private static int Int(RegistryKey key, string name, int fallback) =>
        key.GetValue(name) is int value ? value : fallback;
}
