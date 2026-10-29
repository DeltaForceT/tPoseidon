using tPoseidon.Hosting;

namespace tPoseidon.LauncherKit;

public sealed class Branding
{
    private Branding(BrandingSpec spec, string folder)
    {
        Spec = spec;
        Folder = folder;
        Server = spec.Server;
    }

    public BrandingSpec Spec { get; }

    public string Folder { get; }

    public string LogoPath => Path.Combine(Folder, Spec.Logo);

    public string? BackgroundPath => Spec.Background is null ? null : Path.Combine(Folder, Spec.Background);

    public string IconPath => Path.Combine(Folder, Spec.Icon);

    public ServerFile Server { get; }

    public string SettingsFolderName => FileNames.Safe(Spec.ServerName);

    public static Branding? Find(string launcherFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherFolder);
        var folder = Path.Combine(launcherFolder, BrandingSpec.FolderName);
        return Directory.Exists(folder) ? Load(folder) : null;
    }

    public static Branding Load(string folder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var full = Path.GetFullPath(folder);
        var file = Path.Combine(full, BrandingSpec.FileName);
        string json;
        try
        {
            json = File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The launcher branding could not be read ({ex.Message}).", "Install the launcher again.", ex);
        }

        var spec = BrandingSpec.Parse(json);
        Require(Path.Combine(full, spec.Logo), BrandingSpec.MaxLogoBytes, "logo");
        Require(Path.Combine(full, spec.Icon), BrandingSpec.MaxIconBytes, "icon");
        if (spec.Background is not null)
        {
            Require(Path.Combine(full, spec.Background), BrandingSpec.MaxBackgroundBytes, "background");
        }

        return new Branding(spec, full);
    }

    public static Branding Write(string folder, BrandingSpec spec, byte[] logo, byte[]? background, byte[] icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(logo);
        ArgumentNullException.ThrowIfNull(icon);
        var valid = spec.Validated();
        if ((valid.Background is null) != (background is null))
        {
            throw new HostingException("The background picture and its file name must go together.");
        }

        var full = Path.GetFullPath(folder);
        try
        {
            Directory.CreateDirectory(full);
            File.WriteAllBytes(Path.Combine(full, valid.Logo), logo);
            File.WriteAllBytes(Path.Combine(full, valid.Icon), icon);
            if (valid.Background is not null && background is not null)
            {
                File.WriteAllBytes(Path.Combine(full, valid.Background), background);
            }

            File.WriteAllText(Path.Combine(full, BrandingSpec.FileName), valid.ToJson() + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new HostingException($"The launcher branding could not be written to {full}: {ex.Message}", ex);
        }

        return Load(full);
    }

    private static void Require(string path, long maxBytes, string role)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new HostingException($"The launcher's {role} picture {info.Name} is missing.", "Install the launcher again.", null);
        }

        if (info.Length == 0 || info.Length > maxBytes)
        {
            throw new HostingException($"The launcher's {role} picture {info.Name} has a wrong size.", "Install the launcher again.", null);
        }
    }
}

public static class FileNames
{
    public static string Safe(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Trim().Select(c => invalid.Contains(c) || char.IsControl(c) ? ' ' : c).ToArray());
        cleaned = string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.');
        return cleaned.Length == 0 ? "Launcher" : cleaned;
    }
}
