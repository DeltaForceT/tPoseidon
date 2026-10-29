using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using tPoseidon.Hosting;
using tPoseidon.LauncherKit;
using tPoseidon.Ui;

namespace tPoseidon.Launcher.Installer;

public static class Program
{
    public const string RemoveArgument = "/remove";

    [STAThread]
    public static int Main(string[] args)
    {
        Arguments = args;
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static string[] Arguments { get; private set; } = [];

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();
}

public sealed class App : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Skins.Register(Styles);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (Open(Program.Arguments) is { } window)
            {
                desktop.MainWindow = window;
            }
            else
            {
                desktop.Shutdown();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static SetupWindow? Open(string[] args)
    {
        try
        {
            var executable = Environment.ProcessPath ?? throw new InstallerException("The installer cannot find itself.");
            var payload = InstallerPayload.TryRead(executable);
            if (payload is not null)
            {
                return InstallMode(payload);
            }

            if (args.Length == 1 && args[0] == InstallPlan.UninstallArgument)
            {
                Relaunch(executable);
                return null;
            }

            if (args.Length == 2 && args[0] == Program.RemoveArgument)
            {
                return RemoveMode(args[1]);
            }

            throw new InstallerException("This program has no launcher to install. Build the launcher again in the control panel.");
        }
        catch (Exception ex) when (ex is InstallerException or HostingException or IOException or InvalidDataException)
        {
            return SetupWindow.Failure(ex.Message);
        }
    }

    private static SetupWindow InstallMode(InstallerPayload payload)
    {
        var header = payload.Header;
        var plan = InstallPlan.Create(header, InstallLocations.ForCurrentUser());
        var logo = ReadEntry(payload, header.Logo);
        var icon = ReadEntry(payload, header.Icon);
        var services = Services(payload);
        return new SetupWindow(
            $"{header.ServerName} Setup",
            logo,
            icon,
            header.AccentColor,
            $"Install {header.ServerName} for your Windows account.",
            plan.Folder,
            "Install",
            progress =>
            {
                using var zip = payload.OpenZip();
                Installer.Install(plan, zip, services, progress);
                return $"{header.ServerName} is installed. Use the shortcut on your desktop.";
            },
            ("Start now", () => Process.Start(new ProcessStartInfo(plan.ExecutablePath) { UseShellExecute = true, WorkingDirectory = plan.Folder })?.Dispose()));
    }

    private static SetupWindow RemoveMode(string folder)
    {
        var plan = InstallPlan.Read(Path.Combine(folder, InstallPlan.RecordFileName));
        var services = Services(null);
        var window = new SetupWindow(
            $"Remove {plan.Uninstall.DisplayName}",
            null,
            null,
            "#5B9DFF",
            $"Remove {plan.Uninstall.DisplayName} from this computer. Your settings and your game client are not touched.",
            plan.Folder,
            "Remove",
            _ =>
            {
                Installer.Uninstall(plan, services);
                return $"{plan.Uninstall.DisplayName} was removed.";
            },
            null);
        window.Closed += (_, _) => RemoveWorkFolder();
        return window;
    }

    private static void RemoveWorkFolder()
    {
        var work = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
        var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("/c");
        info.ArgumentList.Add($"ping -n 3 127.0.0.1 >nul & rmdir /s /q \"{work}\"");
        Process.Start(info)?.Dispose();
    }

    private static void Relaunch(string executable)
    {
        var folder = Path.GetDirectoryName(executable)!;
        var work = Path.Combine(Path.GetTempPath(), "tposeidon-uninstall-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var copy = Path.Combine(work, InstallPlan.UninstallerFileName);
        File.Copy(executable, copy);
        var info = new ProcessStartInfo(copy) { UseShellExecute = false, WorkingDirectory = work };
        info.ArgumentList.Add(Program.RemoveArgument);
        info.ArgumentList.Add(folder);
        Process.Start(info)?.Dispose();
    }

    private static InstallerServices Services(InstallerPayload? payload)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InstallerException("This installer is for Windows.");
        }

        return new InstallerServices(new DiskInstallFiles(payload), new ProcessRunningCheck(), new WindowsShortcutWriter(), new WindowsUninstallRegistry());
    }

    private static byte[]? ReadEntry(InstallerPayload payload, string path)
    {
        using var zip = new System.IO.Compression.ZipArchive(payload.OpenZip(), System.IO.Compression.ZipArchiveMode.Read);
        var entry = zip.GetEntry(path);
        if (entry is null)
        {
            return null;
        }

        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
