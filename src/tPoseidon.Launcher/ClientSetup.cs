using System.Runtime.Versioning;
using System.Security;
using System.Text;
using Microsoft.Win32;
using tPoseidon.Hosting;

namespace tPoseidon.Launcher;

public sealed record ResolutionChoice(int Width, int Height)
{
    public string Label => $"{Width} x {Height}";
}

public sealed record QualityChoice(int Value, string Label);

public sealed record SoundChannelChoice(string Label, int Samples2D, int Samples3D, int SamplesStream);

public sealed record ClientSetup(
    int Width,
    int Height,
    bool FullScreen,
    bool UseSound,
    int SoundFrequency,
    int SoundBits,
    int ChannelChoice,
    bool Use3DEffect,
    bool UseMapEffect,
    string ScreenShotFormat,
    int JpgQuality)
{
    public static readonly IReadOnlyList<ResolutionChoice> Resolutions = [new(800, 600), new(1024, 768)];

    public static readonly IReadOnlyList<int> SampleRates = [22050, 44100];

    public static readonly IReadOnlyList<int> BitDepths = [8, 16];

    public static readonly IReadOnlyList<SoundChannelChoice> Channels =
    [
        new("8 Channel", 4, 4, 1),
        new("16 Channel", 8, 8, 2),
        new("32 Channel", 16, 16, 2),
        new("48 Channel", 32, 16, 2),
    ];

    public static readonly IReadOnlyList<string> ScreenShotFormats = ["BMP", "JPG"];

    public static readonly IReadOnlyList<QualityChoice> Qualities =
    [
        new(50, "Low"),
        new(75, "Middle"),
        new(85, "High"),
        new(100, "Very High"),
    ];

    public static ClientSetup Defaults { get; } = new(1024, 768, false, true, 44100, 16, 1, true, true, "JPG", 85);

    public bool IsValid =>
        Resolutions.Any(choice => choice.Width == Width && choice.Height == Height)
        && SampleRates.Contains(SoundFrequency)
        && BitDepths.Contains(SoundBits)
        && ChannelChoice >= 0 && ChannelChoice < Channels.Count
        && ScreenShotFormats.Contains(ScreenShotFormat, StringComparer.Ordinal)
        && Qualities.Any(choice => choice.Value == JpgQuality);

    public SoundChannelChoice Sound => Channels[ChannelChoice];
}

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

public interface IClientSetupStore
{
    ClientSetup Load(ClientSetupProfile profile);

    void Save(ClientSetupProfile profile, ClientSetup setup);
}

[SupportedOSPlatform("windows")]
public sealed class RegistryClientSetupStore : IClientSetupStore
{
    public const string Width = "widthPixel";
    public const string Height = "heightPixel";
    public const string FullScreen = "Full Screen";
    public const string UseSound = "UseSound";
    public const string SoundStereo = "SoundStereo";
    public const string SoundFrequency = "SoundFrequency";
    public const string SoundBit = "SoundBit";
    public const string Samples2D = "2DSample";
    public const string Samples3D = "3DSample";
    public const string SamplesStream = "StreamSample";
    public const string Use3DEffect = "Use3DEffect";
    public const string UseMapEffect = "UseMapEffect";
    public const string CaptureFormat = "CaptureScreenFormat";
    public const string CaptureQuality = "CaptureScreenJPGQuality";

    public ClientSetup Load(ClientSetupProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var defaults = ClientSetup.Defaults;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(profile.KeyPath);
            if (key is null)
            {
                return defaults;
            }

            var width = Int(key, Width, defaults.Width);
            var resolution = ClientSetup.Resolutions.FirstOrDefault(choice => choice.Width == width) ?? ClientSetup.Resolutions[^1];
            var triple = (Int(key, Samples2D, -1), Int(key, Samples3D, -1), Int(key, SamplesStream, -1));
            var channel = ClientSetup.Channels.ToList().FindIndex(choice => (choice.Samples2D, choice.Samples3D, choice.SamplesStream) == triple);
            var format = key.GetValue(CaptureFormat) as string;
            return new ClientSetup(
                resolution.Width,
                resolution.Height,
                Int(key, FullScreen, 0) == 1,
                Int(key, UseSound, 1) == 1,
                Pick(ClientSetup.SampleRates, Int(key, SoundFrequency, defaults.SoundFrequency), defaults.SoundFrequency),
                Pick(ClientSetup.BitDepths, Int(key, SoundBit, defaults.SoundBits), defaults.SoundBits),
                channel < 0 ? defaults.ChannelChoice : channel,
                Int(key, Use3DEffect, 1) != 0,
                Int(key, UseMapEffect, 1) != 0,
                ClientSetup.ScreenShotFormats.Contains(format, StringComparer.Ordinal) ? format! : defaults.ScreenShotFormat,
                ClientSetup.Qualities.Any(choice => choice.Value == Int(key, CaptureQuality, -1)) ? Int(key, CaptureQuality, -1) : defaults.JpgQuality);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            throw new HostingException($"The client settings could not be read: {ex.Message}", ex);
        }
    }

    public void Save(ClientSetupProfile profile, ClientSetup setup)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(setup);
        if (!setup.IsValid)
        {
            throw new HostingException("These client settings are not ones the client offers.");
        }

        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(profile.KeyPath, writable: true);
            key.SetValue(FullScreen, setup.FullScreen ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(UseMapEffect, setup.UseMapEffect ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(Use3DEffect, setup.Use3DEffect ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(SoundFrequency, setup.SoundFrequency, RegistryValueKind.DWord);
            key.SetValue(SoundBit, setup.SoundBits, RegistryValueKind.DWord);
            key.SetValue(Samples2D, setup.Sound.Samples2D, RegistryValueKind.DWord);
            key.SetValue(Samples3D, setup.Sound.Samples3D, RegistryValueKind.DWord);
            key.SetValue(SamplesStream, setup.Sound.SamplesStream, RegistryValueKind.DWord);
            key.SetValue(UseSound, setup.UseSound ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(SoundStereo, setup.UseSound ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue(CaptureFormat, setup.ScreenShotFormat, RegistryValueKind.String);
            key.SetValue(CaptureQuality, setup.JpgQuality, RegistryValueKind.DWord);
            key.SetValue(Width, setup.Width, RegistryValueKind.DWord);
            key.SetValue(Height, setup.Height, RegistryValueKind.DWord);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            throw new HostingException($"The client settings could not be saved: {ex.Message}", ex);
        }
    }

    private static int Int(RegistryKey key, string name, int fallback) =>
        key.GetValue(name) is int value ? value : fallback;

    private static int Pick(IReadOnlyList<int> choices, int value, int fallback) =>
        choices.Contains(value) ? value : fallback;
}
