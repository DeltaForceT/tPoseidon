using System.Text.Json;
using tPoseidon.LauncherKit;

namespace tPoseidon.Launcher.Installer;

public sealed record InstallLocations(string ProgramsRoot, string StartMenuPrograms, string Desktop)
{
    public static InstallLocations ForCurrentUser() => new(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs"),
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
}

public sealed record ShortcutPlan(string Path, string Target, string IconPath, string Description, string WorkingDirectory);

public sealed record UninstallEntry(string KeyPath, string DisplayName, string DisplayVersion, string Publisher, string InstallLocation, string DisplayIcon, string UninstallString);

public sealed record InstallPlan(string Folder, string ExecutablePath, string UninstallerPath, IReadOnlyList<ShortcutPlan> Shortcuts, UninstallEntry Uninstall)
{
    public const string UninstallerFileName = "Uninstall.exe";
    public const string RecordFileName = "install.json";
    public const string UninstallRoot = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";
    public const string UninstallArgument = "/uninstall";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string RecordPath => Path.Combine(Folder, RecordFileName);

    public static InstallPlan Create(InstallerHeader header, InstallLocations locations)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(locations);
        var name = FileNames.Safe(header.ServerName);
        var folder = Path.Combine(locations.ProgramsRoot, name);
        var executable = Path.Combine(folder, header.Executable);
        var icon = Path.Combine(folder, header.Icon.Replace('/', Path.DirectorySeparatorChar));
        var uninstaller = Path.Combine(folder, UninstallerFileName);
        var shortcuts = new[] { locations.StartMenuPrograms, locations.Desktop }
            .Select(directory => new ShortcutPlan(Path.Combine(directory, name + ".lnk"), executable, icon, header.ServerName, folder))
            .ToList();
        var entry = new UninstallEntry(
            $@"{UninstallRoot}\{name.Replace(' ', '_')}_Launcher",
            header.ServerName,
            header.Version,
            header.ServerName,
            folder,
            icon,
            $"\"{uninstaller}\" {UninstallArgument}");
        return new InstallPlan(folder, executable, uninstaller, shortcuts, entry);
    }

    public static InstallPlan Read(string recordPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordPath);
        return JsonSerializer.Deserialize<InstallPlan>(File.ReadAllText(recordPath), JsonOptions)
            ?? throw new InvalidDataException($"{recordPath} is not an install record.");
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
}
